using AIDocumentMeetingAssistant.API.Helpers;
using AIDocumentMeetingAssistant.API.Models;
using AIDocumentMeetingAssistant.API.Services;
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
    public class ExportController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IExportService _exportService;

        public ExportController(AppDbContext context, IExportService exportService)
        {
            _context = context;
            _exportService = exportService;
        }

        /// <summary>
        /// GET /api/export/pdf/{type}/{id} - Export au format PDF (Vérification du rôle User vs Admin)
        /// </summary>
        [HttpGet("pdf/{type}/{id}")]
        public async Task<IActionResult> ExportPdf(string type, Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var exportData = await BuildExportDataAsync(type, id, cancellationToken);
                if (exportData == null)
                {
                    return NotFound(new { message = $"Impossible d'exporter en PDF: Aucun élément trouvé pour le type '{type}' et l'ID '{id}'." });
                }

                var pdfBytes = await _exportService.GeneratePdfAsync(exportData, cancellationToken);
                string fileName = $"{SanitizeFileName(exportData.FileName)}.pdf";

                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur lors de l'exportation PDF: {ex.Message}" });
            }
        }

        /// <summary>
        /// GET /api/export/word/{type}/{id} - Export au format Word (.docx)
        /// </summary>
        [HttpGet("word/{type}/{id}")]
        public async Task<IActionResult> ExportWord(string type, Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var exportData = await BuildExportDataAsync(type, id, cancellationToken);
                if (exportData == null)
                {
                    return NotFound(new { message = $"Impossible d'exporter en Word: Aucun élément trouvé pour le type '{type}' et l'ID '{id}'." });
                }

                var wordBytes = await _exportService.GenerateWordAsync(exportData, cancellationToken);
                string fileName = $"{SanitizeFileName(exportData.FileName)}.docx";

                return File(wordBytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur lors de l'exportation Word: {ex.Message}" });
            }
        }

        // ================= MÉTHODES PRIVÉES DE CONSTRUCTION DES DONNÉES =================

        private async Task<ExportDataModel?> BuildExportDataAsync(string type, Guid id, CancellationToken cancellationToken)
        {
            string normalizedType = type.Trim().ToLowerInvariant();

            switch (normalizedType)
            {
                case "summary":
                case "aisummary":
                case "minutes":
                    return await BuildSummaryExportAsync(id, cancellationToken);

                case "meeting":
                case "reunion":
                    return await BuildMeetingExportAsync(id, cancellationToken);

                case "document":
                case "doc":
                    return await BuildDocumentExportAsync(id, cancellationToken);

                case "chat":
                case "qa":
                case "question":
                case "agent":
                    return await BuildQuestionExportAsync(id, cancellationToken);

                default:
                    var summary = await _context.Aisummaries.Include(s => s.Meeting).FirstOrDefaultAsync(s => s.Aisummary_Id == id, cancellationToken);
                    if (summary != null) return await BuildSummaryExportAsync(id, cancellationToken);

                    var meeting = await _context.Meetings.FirstOrDefaultAsync(m => m.Meeting_Id == id, cancellationToken);
                    if (meeting != null) return await BuildMeetingExportAsync(id, cancellationToken);

                    var document = await _context.Documents.FirstOrDefaultAsync(d => d.Document_Id == id, cancellationToken);
                    if (document != null) return await BuildDocumentExportAsync(id, cancellationToken);

                    var question = await _context.Questions.FirstOrDefaultAsync(q => q.Question_Id == id, cancellationToken);
                    if (question != null) return await BuildQuestionExportAsync(id, cancellationToken);

                    return null;
            }
        }

        private void CheckAccessPermission(Guid? ownerUserId)
        {
            bool isAdmin = UserContextHelper.IsAdmin(User);
            var currentUserId = UserContextHelper.GetUserId(User);

            if (!isAdmin && currentUserId.HasValue && ownerUserId.HasValue && ownerUserId.Value != currentUserId.Value)
            {
                throw new UnauthorizedAccessException("Accès refusé. Vous ne pouvez exporter que vos propres ressources.");
            }
        }

        private async Task<ExportDataModel?> BuildSummaryExportAsync(Guid id, CancellationToken cancellationToken)
        {
            var summary = await _context.Aisummaries
                .Include(s => s.Meeting)
                .FirstOrDefaultAsync(s => s.Aisummary_Id == id, cancellationToken);

            if (summary == null)
            {
                summary = await _context.Aisummaries
                    .Include(s => s.Meeting)
                    .OrderByDescending(s => s.Aisummary_CreatedAt)
                    .FirstOrDefaultAsync(s => s.Meeting_Id == id, cancellationToken);
            }

            if (summary == null) return null;

            CheckAccessPermission(summary.Meeting?.CreatedById);

            bool isMinutes = summary.Type.Equals("minutes", StringComparison.OrdinalIgnoreCase);
            string categoryName = isMinutes ? "Compte-Rendu de Réunion" : "Résumé IA";
            string titleStr = isMinutes ? $"Compte-Rendu : {summary.Meeting?.Meeting_Title ?? "Réunion"}" : $"Résumé IA";

            var model = new ExportDataModel
            {
                Title = titleStr,
                Subtitle = summary.Meeting != null ? $"Réunion rattachée : {summary.Meeting.Meeting_Title}" : null,
                Category = categoryName,
                CreatedAt = summary.Aisummary_CreatedAt,
                FileName = $"Export_{categoryName}_{summary.Aisummary_Id.ToString().Substring(0, 8)}"
            };

            if (summary.Meeting != null)
            {
                model.Metadata["Réunion"] = summary.Meeting.Meeting_Title;
                model.Metadata["Date Réunion"] = summary.Meeting.MeetingDate.ToString("dd/MM/yyyy HH:mm");
                if (!string.IsNullOrWhiteSpace(summary.Meeting.Participants))
                {
                    model.Metadata["Participants"] = summary.Meeting.Participants;
                }
            }

            model.Sections.Add(new ExportSectionModel
            {
                Heading = isMinutes ? "Compte-Rendu Officiel" : "Résumé Synthétique",
                Content = summary.Content
            });

            return model;
        }

        private async Task<ExportDataModel?> BuildMeetingExportAsync(Guid id, CancellationToken cancellationToken)
        {
            var meeting = await _context.Meetings
                .Include(m => m.CreatedBy)
                .Include(m => m.MeetingNotes)
                .Include(m => m.Aisummaries)
                .Include(m => m.Documents)
                .FirstOrDefaultAsync(m => m.Meeting_Id == id, cancellationToken);

            if (meeting == null) return null;

            CheckAccessPermission(meeting.CreatedById);

            var model = new ExportDataModel
            {
                Title = meeting.Meeting_Title,
                Subtitle = $"Réunion du {meeting.MeetingDate:dd/MM/yyyy HH:mm}",
                Category = "Fiche de Réunion Complète",
                CreatedAt = DateTime.UtcNow,
                FileName = $"Meeting_{meeting.Meeting_Title}"
            };

            model.Metadata["Titre Réunion"] = meeting.Meeting_Title;
            model.Metadata["Date & Heure"] = meeting.MeetingDate.ToString("dd/MM/yyyy HH:mm");
            model.Metadata["Organisateur"] = meeting.CreatedBy?.FullName ?? meeting.CreatedBy?.UserName ?? "Non spécifié";
            model.Metadata["Participants"] = !string.IsNullOrWhiteSpace(meeting.Participants) ? meeting.Participants : "Aucun participant renseigné";

            var minutesSummary = meeting.Aisummaries.FirstOrDefault(s => s.Type == "minutes") ?? meeting.Aisummaries.FirstOrDefault();
            if (minutesSummary != null)
            {
                model.Sections.Add(new ExportSectionModel
                {
                    Heading = "Compte-Rendu Généré par l'IA",
                    Content = minutesSummary.Content
                });
            }

            if (meeting.MeetingNotes.Any())
            {
                string combinedNotes = string.Join("\n\n---\n\n", meeting.MeetingNotes.Select(n => n.NotesContent));
                model.Sections.Add(new ExportSectionModel
                {
                    Heading = "Notes Manuelles de la Réunion",
                    Content = combinedNotes
                });
            }

            if (meeting.Documents.Any())
            {
                string docsList = string.Join("\n", meeting.Documents.Select(d => $"- {d.Document_FileName} ({d.Document_FileType}, {FormatBytes(d.FileSize)})"));
                model.Sections.Add(new ExportSectionModel
                {
                    Heading = "Documents Rattachés",
                    Content = docsList
                });
            }

            return model;
        }

        private async Task<ExportDataModel?> BuildDocumentExportAsync(Guid id, CancellationToken cancellationToken)
        {
            var doc = await _context.Documents
                .Include(d => d.Meeting)
                .FirstOrDefaultAsync(d => d.Document_Id == id, cancellationToken);

            if (doc == null) return null;

            CheckAccessPermission(doc.Meeting?.CreatedById);

            var model = new ExportDataModel
            {
                Title = doc.Document_FileName,
                Subtitle = $"Type : {doc.Document_FileType}",
                Category = "Document & Extraction Textuelle",
                CreatedAt = doc.UploadedAt,
                FileName = $"Doc_{doc.Document_FileName}"
            };

            model.Metadata["Nom Fichier"] = doc.Document_FileName;
            model.Metadata["Format"] = doc.Document_FileType;
            model.Metadata["Taille Fichier"] = FormatBytes(doc.FileSize);
            model.Metadata["Date d'Upload"] = doc.UploadedAt.ToString("dd/MM/yyyy HH:mm");

            if (!string.IsNullOrWhiteSpace(doc.Document_Description))
            {
                model.Metadata["Description"] = doc.Document_Description;
            }

            if (doc.Meeting != null)
            {
                model.Metadata["Réunion Liée"] = doc.Meeting.Meeting_Title;
            }

            if (!string.IsNullOrWhiteSpace(doc.ExtractedText))
            {
                model.Sections.Add(new ExportSectionModel
                {
                    Heading = "Texte Extrait du Document",
                    Content = doc.ExtractedText
                });
            }
            else
            {
                model.Sections.Add(new ExportSectionModel
                {
                    Heading = "Statut d'Extraction",
                    Content = "Aucun texte extrait disponible pour ce document."
                });
            }

            return model;
        }

        private async Task<ExportDataModel?> BuildQuestionExportAsync(Guid id, CancellationToken cancellationToken)
        {
            var question = await _context.Questions
                .Include(q => q.Answer)
                .Include(q => q.Meeting)
                .FirstOrDefaultAsync(q => q.Question_Id == id, cancellationToken);

            if (question == null) return null;

            CheckAccessPermission(question.Meeting?.CreatedById);

            var model = new ExportDataModel
            {
                Title = "Échange avec l'Agent IA Polia",
                Subtitle = question.Meeting != null ? $"Contexte Réunion : {question.Meeting.Meeting_Title}" : null,
                Category = "Question & Réponse IA",
                CreatedAt = question.CreatedAt,
                FileName = $"Question_Polia_{question.Question_Id.ToString().Substring(0, 8)}"
            };

            model.Metadata["Date de l'échange"] = question.CreatedAt.ToString("dd/MM/yyyy HH:mm");
            if (question.Meeting != null)
            {
                model.Metadata["Réunion Associée"] = question.Meeting.Meeting_Title;
            }

            model.Sections.Add(new ExportSectionModel
            {
                Heading = "Question Posée à Polia",
                Content = question.Question_Content
            });

            model.Sections.Add(new ExportSectionModel
            {
                Heading = "Réponse Générée par Polia",
                Content = question.Answer?.Answer_Content ?? "Aucune réponse enregistrée."
            });

            return model;
        }

        private static string FormatBytes(long? bytes)
        {
            if (!bytes.HasValue || bytes == 0) return "0 Octets";
            string[] suffixes = { "Octets", "KO", "MO", "GO" };
            int i = 0;
            double dblSByte = bytes.Value;
            while (dblSByte >= 1024 && i < suffixes.Length - 1)
            {
                dblSByte /= 1024;
                i++;
            }
            return $"{dblSByte:0.##} {suffixes[i]}";
        }

        private static string SanitizeFileName(string fileName)
        {
            var invalidChars = System.IO.Path.GetInvalidFileNameChars();
            var clean = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
            return string.IsNullOrWhiteSpace(clean) ? "Export" : clean.Replace(" ", "_");
        }
    }
}
