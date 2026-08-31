using AIDocumentMeetingAssistant.API.DTOs;
using AIDocumentMeetingAssistant.API.Helpers;
using AIDocumentMeetingAssistant.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIDocumentMeetingAssistant.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class HistoryController : ControllerBase
    {
        private readonly AppDbContext _context;

        public HistoryController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// GET /api/history - Historique des contenus IA (Partitionné par rôle : User voit uniquement ses données, Admin voit tout)
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<HistoryResponseDto>> GetHistory(
            [FromQuery] string? category,
            [FromQuery] Guid? meetingId,
            [FromQuery] string? search,
            CancellationToken cancellationToken = default)
        {
            try
            {
                bool isAdmin = UserContextHelper.IsAdmin(User);
                var currentUserId = UserContextHelper.GetUserId(User);

                var historyItems = new List<HistoryItemDto>();

                // 1. Récupération des résumés et comptes-rendus IA (Aisummaries)
                var summariesQuery = _context.Aisummaries
                    .Include(s => s.Meeting)
                    .AsQueryable();

                if (!isAdmin && currentUserId.HasValue)
                {
                    summariesQuery = summariesQuery.Where(s => s.Meeting != null && s.Meeting.CreatedById == currentUserId.Value);
                }

                if (meetingId.HasValue && meetingId.Value != Guid.Empty)
                {
                    summariesQuery = summariesQuery.Where(s => s.Meeting_Id == meetingId.Value);
                }

                var summaries = await summariesQuery.ToListAsync(cancellationToken);

                foreach (var s in summaries)
                {
                    string cat = s.Type.Equals("minutes", StringComparison.OrdinalIgnoreCase) ? "Minutes" : "Summary";
                    string title = s.Type.Equals("minutes", StringComparison.OrdinalIgnoreCase)
                        ? $"Compte-rendu : {s.Meeting?.Meeting_Title ?? "Réunion"}"
                        : $"Résumé IA : {s.Meeting?.Meeting_Title ?? "Document / Réunion"}";

                    historyItems.Add(new HistoryItemDto
                    {
                        Id = s.Aisummary_Id,
                        Category = cat,
                        Type = s.Type,
                        Title = title,
                        Content = s.Content,
                        MeetingId = s.Meeting_Id,
                        MeetingTitle = s.Meeting?.Meeting_Title,
                        CreatedAt = s.Aisummary_CreatedAt
                    });
                }

                // 2. Récupération des échanges Questions / Réponses (Polia Agent IA)
                var questionsQuery = _context.Questions
                    .Include(q => q.Answer)
                    .Include(q => q.Meeting)
                    .AsQueryable();

                if (!isAdmin && currentUserId.HasValue)
                {
                    questionsQuery = questionsQuery.Where(q => q.Meeting != null && q.Meeting.CreatedById == currentUserId.Value);
                }

                if (meetingId.HasValue && meetingId.Value != Guid.Empty)
                {
                    questionsQuery = questionsQuery.Where(q => q.Meeting_Id == meetingId.Value);
                }

                var questions = await questionsQuery.ToListAsync(cancellationToken);

                foreach (var q in questions)
                {
                    historyItems.Add(new HistoryItemDto
                    {
                        Id = q.Question_Id,
                        Category = "Q&A",
                        Type = "chat",
                        Title = $"Question : {q.Question_Content}",
                        Content = q.Answer?.Answer_Content ?? "Pas de réponse enregistrée.",
                        MeetingId = q.Meeting_Id,
                        MeetingTitle = q.Meeting?.Meeting_Title,
                        CreatedAt = q.CreatedAt
                    });
                }

                // 3. Filtrage optionnel par catégorie/type
                if (!string.IsNullOrWhiteSpace(category))
                {
                    historyItems = historyItems.Where(item =>
                        item.Category.Equals(category, StringComparison.OrdinalIgnoreCase) ||
                        item.Type.Equals(category, StringComparison.OrdinalIgnoreCase)
                    ).ToList();
                }

                // 4. Filtrage par mot-clé de recherche
                if (!string.IsNullOrWhiteSpace(search))
                {
                    string searchLower = search.ToLowerInvariant();
                    historyItems = historyItems.Where(item =>
                        item.Title.ToLowerInvariant().Contains(searchLower) ||
                        item.Content.ToLowerInvariant().Contains(searchLower) ||
                        (item.MeetingTitle != null && item.MeetingTitle.ToLowerInvariant().Contains(searchLower))
                    ).ToList();
                }

                // 5. Tri chronologique décroissant
                historyItems = historyItems.OrderByDescending(item => item.CreatedAt).ToList();

                return Ok(new HistoryResponseDto
                {
                    Items = historyItems,
                    TotalCount = historyItems.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur lors de la récupération de l'historique IA: {ex.Message}");
            }
        }
    }
}
