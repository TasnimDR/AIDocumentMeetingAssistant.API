using AIDocumentMeetingAssistant.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIDocumentMeetingAssistant.API.Controllers
{
    /// <summary>
    /// Contrôleur d'administration pour les statistiques globales du système et paramètres
    /// Accessibilité : Administrateurs uniquement ([Authorize(Roles = "Admin")])
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class StatsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public StatsController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        /// <summary>
        /// GET: api/stats - Statistiques globales du système et métriques globales pour l'Administrateur
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetGlobalStatistics(CancellationToken cancellationToken)
        {
            try
            {
                // Métriques Utilisateurs
                var totalUsers = await _context.Users.CountAsync(cancellationToken);
                var activeUsers = await _context.Users.CountAsync(u => u.IsActive, cancellationToken);
                var adminUsers = await _context.Users
                    .Include(u => u.Role)
                    .CountAsync(u => u.Role != null && u.Role.Role_Name == "Admin", cancellationToken);

                // Métriques Réunions & Documents
                var totalMeetings = await _context.Meetings.CountAsync(cancellationToken);
                var totalDocuments = await _context.Documents.CountAsync(cancellationToken);
                var totalStorageBytes = await _context.Documents.SumAsync(d => d.FileSize ?? 0, cancellationToken);

                // Métriques Contenus IA
                var totalAiSummaries = await _context.Aisummaries.CountAsync(cancellationToken);
                var totalQuestions = await _context.Questions.CountAsync(cancellationToken);

                return Ok(new
                {
                    users = new
                    {
                        total = totalUsers,
                        active = activeUsers,
                        admins = adminUsers,
                        standardUsers = totalUsers - adminUsers
                    },
                    meetings = new
                    {
                        total = totalMeetings
                    },
                    documents = new
                    {
                        total = totalDocuments,
                        totalStorageBytes = totalStorageBytes,
                        totalStorageFormatted = FormatBytes(totalStorageBytes)
                    },
                    aiContent = new
                    {
                        totalSummaries = totalAiSummaries,
                        totalQuestionsAndAnswers = totalQuestions
                    },
                    systemParameters = new
                    {
                        status = "Operationnel & Prêt",
                        ollamaModel = _configuration["Ollama:Model"] ?? "qwen2.5",
                        ollamaBaseUrl = _configuration["Ollama:BaseUrl"] ?? "http://localhost:11434",
                        qdrantCollection = _configuration["Qdrant:CollectionName"] ?? "documents",
                        qdrantBaseUrl = _configuration["Qdrant:BaseUrl"] ?? "http://localhost:6333",
                        databaseStatus = "Connectée (SQL Server)"
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur lors du calcul des statistiques globales: {ex.Message}" });
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes == 0) return "0 Octets";
            string[] suffixes = { "Octets", "KO", "MO", "GO", "TO" };
            int i = 0;
            double dblSByte = bytes;
            while (dblSByte >= 1024 && i < suffixes.Length - 1)
            {
                dblSByte /= 1024;
                i++;
            }
            return $"{dblSByte:0.##} {suffixes[i]}";
        }
    }
}
