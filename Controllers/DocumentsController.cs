using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AIDocumentMeetingAssistant.API.Models;
using AIDocumentMeetingAssistant.API.Services;
using AIDocumentMeetingAssistant.API.DTOs;
using AIDocumentMeetingAssistant.API.Helpers;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIDocumentMeetingAssistant.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DocumentsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ITextExtractionService _textExtraction;
        private readonly IWebHostEnvironment _environment;
        private readonly IOllamaService _ollamaService;
        private readonly IQdrantService _qdrantService;

        public DocumentsController(
            AppDbContext context,
            ITextExtractionService textExtraction,
            IWebHostEnvironment environment,
            IOllamaService ollamaService,
            IQdrantService qdrantService)
        {
            _context = context;
            _textExtraction = textExtraction;
            _environment = environment;
            _ollamaService = ollamaService;
            _qdrantService = qdrantService;
        }

        // GET: api/documents
        [HttpGet]
        public async Task<ActionResult<DocumentResponseDto>> GetDocuments()
        {
            bool isAdmin = UserContextHelper.IsAdmin(User);
            var currentUserId = UserContextHelper.GetUserId(User);

            var query = _context.Documents
                .Include(d => d.Meeting)
                .AsQueryable();

            if (!isAdmin && currentUserId.HasValue)
            {
                query = query.Where(d => d.Meeting != null && d.Meeting.CreatedById == currentUserId.Value);
            }

            var documents = await query
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();

            var documentDtos = documents.Select(d => new DocumentDto
            {
                Document_Id = d.Document_Id,
                Document_FileName = d.Document_FileName,
                Document_FileType = d.Document_FileType,
                Document_Description = d.Document_Description,
                ExtractedText = d.ExtractedText,
                FileSize = d.FileSize,
                UploadedAt = d.UploadedAt,
                Status = IsTextReallyExtracted(d.ExtractedText) ? "extrait" : "en_attente",
                Preview = IsTextReallyExtracted(d.ExtractedText) && !string.IsNullOrEmpty(d.ExtractedText)
                    ? d.ExtractedText.Length > 100
                        ? d.ExtractedText.Substring(0, 100) + "..."
                        : d.ExtractedText
                    : null
            }).ToList();

            var response = new DocumentResponseDto
            {
                Documents = documentDtos,
                TotalCount = documentDtos.Count,
                TotalSize = documentDtos.Sum(d => d.FileSize ?? 0),
                PendingCount = documentDtos.Count(d => d.Status == "en_attente")
            };

            return Ok(response);
        }

        // GET: api/documents/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<DocumentDto>> GetDocument(Guid id)
        {
            var document = await _context.Documents
                .Include(d => d.Meeting)
                .FirstOrDefaultAsync(d => d.Document_Id == id);

            if (document == null)
            {
                return NotFound();
            }

            bool isAdmin = UserContextHelper.IsAdmin(User);
            var currentUserId = UserContextHelper.GetUserId(User);

            if (!isAdmin && currentUserId.HasValue && document.Meeting != null && document.Meeting.CreatedById.HasValue && document.Meeting.CreatedById.Value != currentUserId.Value)
            {
                return StatusCode(403, "Accès refusé. Vous ne pouvez consulter que vos propres documents.");
            }

            var documentDto = new DocumentDto
            {
                Document_Id = document.Document_Id,
                Document_FileName = document.Document_FileName,
                Document_FileType = document.Document_FileType,
                Document_Description = document.Document_Description,
                ExtractedText = document.ExtractedText,
                FileSize = document.FileSize,
                UploadedAt = document.UploadedAt,
                Status = IsTextReallyExtracted(document.ExtractedText) ? "extrait" : "en_attente",
                Preview = IsTextReallyExtracted(document.ExtractedText) && !string.IsNullOrEmpty(document.ExtractedText)
                    ? document.ExtractedText.Length > 100
                        ? document.ExtractedText.Substring(0, 100) + "..."
                        : document.ExtractedText
                    : null
            };

            return Ok(documentDto);
        }

        // POST: api/documents/upload
        [HttpPost("upload")]
        public async Task<ActionResult<DocumentDto>> UploadDocument([FromForm] DocumentUploadDto uploadDto)
        {
            try
            {
                if (uploadDto.File == null || uploadDto.File.Length == 0)
                {
                    return BadRequest("Aucun fichier n'a été téléchargé.");
                }

                var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".txt" };
                var extension = _textExtraction.GetFileExtension(uploadDto.File.FileName);

                if (!allowedExtensions.Contains(extension))
                {
                    return BadRequest($"Type de fichier non supporté. Types acceptés: {string.Join(", ", allowedExtensions)}");
                }

                var uploadsFolder = Path.Combine(_environment.WebRootPath ?? "wwwroot", "uploads");

                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var fileName = $"{Guid.NewGuid()}_{uploadDto.File.FileName}";
                var filePath = Path.Combine(uploadsFolder, fileName);
                var relativePath = Path.Combine("uploads", fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await uploadDto.File.CopyToAsync(stream);
                }

                var extractedText = await _textExtraction.ExtractTextAsync(uploadDto.File);
                bool isTextExtracted = IsTextReallyExtracted(extractedText);

                var currentUserId = UserContextHelper.GetUserId(User);
                Guid meetingId;

                if (uploadDto.MeetingId.HasValue && uploadDto.MeetingId.Value != Guid.Empty)
                {
                    meetingId = uploadDto.MeetingId.Value;
                    var meetingExists = await _context.Meetings.AnyAsync(m => m.Meeting_Id == meetingId);
                    if (!meetingExists)
                    {
                        var newMeeting = new Meeting
                        {
                            Meeting_Id = meetingId,
                            Meeting_Title = "Meeting par défaut",
                            MeetingDate = DateTime.UtcNow,
                            CreatedAt = DateTime.UtcNow,
                            CreatedById = currentUserId
                        };
                        _context.Meetings.Add(newMeeting);
                        await _context.SaveChangesAsync();
                    }
                }
                else
                {
                    var defaultMeeting = new Meeting
                    {
                        Meeting_Id = Guid.NewGuid(),
                        Meeting_Title = "Documents sans meeting",
                        MeetingDate = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        CreatedById = currentUserId
                    };
                    _context.Meetings.Add(defaultMeeting);
                    await _context.SaveChangesAsync();
                    meetingId = defaultMeeting.Meeting_Id;
                }

                var document = new Document
                {
                    Document_Id = Guid.NewGuid(),
                    Document_FileName = uploadDto.File.FileName,
                    Document_FilePath = relativePath,
                    Document_FileType = extension.TrimStart('.').ToUpper(),
                    Document_Description = uploadDto.Description,
                    ExtractedText = isTextExtracted ? extractedText : null,
                    FileSize = uploadDto.File.Length,
                    Meeting_Id = meetingId,
                    UploadedAt = DateTime.UtcNow
                };

                _context.Documents.Add(document);
                await _context.SaveChangesAsync();

                if (isTextExtracted && !string.IsNullOrEmpty(extractedText))
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _qdrantService.IndexDocumentAsync(document.Document_Id, document.Document_FileName, extractedText, document.Meeting_Id);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Erreur lors de l'indexation Qdrant en arrière-plan: {ex.Message}");
                        }
                    });
                }

                var documentDto = new DocumentDto
                {
                    Document_Id = document.Document_Id,
                    Document_FileName = document.Document_FileName,
                    Document_FileType = document.Document_FileType,
                    Document_Description = document.Document_Description,
                    ExtractedText = document.ExtractedText,
                    FileSize = document.FileSize,
                    UploadedAt = document.UploadedAt,
                    Status = isTextExtracted ? "extrait" : "en_attente",
                    Preview = isTextExtracted && !string.IsNullOrEmpty(extractedText)
                        ? extractedText.Length > 100
                            ? extractedText.Substring(0, 100) + "..."
                            : extractedText
                        : null
                };

                return CreatedAtAction(nameof(GetDocument), new { id = document.Document_Id }, documentDto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur lors du téléchargement: {ex.Message}");
            }
        }

        // POST: api/documents/{id}/extract
        [HttpPost("{id}/extract")]
        public async Task<ActionResult<DocumentDto>> ExtractText(Guid id)
        {
            try
            {
                var document = await _context.Documents
                    .Include(d => d.Meeting)
                    .FirstOrDefaultAsync(d => d.Document_Id == id);

                if (document == null)
                {
                    return NotFound("Document non trouvé.");
                }

                bool isAdmin = UserContextHelper.IsAdmin(User);
                var currentUserId = UserContextHelper.GetUserId(User);

                if (!isAdmin && currentUserId.HasValue && document.Meeting != null && document.Meeting.CreatedById.HasValue && document.Meeting.CreatedById.Value != currentUserId.Value)
                {
                    return StatusCode(403, "Accès refusé. Vous ne pouvez modifier que vos propres documents.");
                }

                var filePath = Path.Combine(_environment.WebRootPath ?? "wwwroot", document.Document_FilePath);
                if (!System.IO.File.Exists(filePath))
                {
                    return BadRequest("Le fichier n'existe pas sur le serveur.");
                }

                using var stream = System.IO.File.OpenRead(filePath);
                var file = new FormFile(stream, 0, stream.Length, document.Document_FileName, document.Document_FileName);

                var extractedText = await _textExtraction.ExtractTextAsync(file);
                bool isTextExtracted = IsTextReallyExtracted(extractedText);

                document.ExtractedText = isTextExtracted ? extractedText : null;
                await _context.SaveChangesAsync();

                if (isTextExtracted && !string.IsNullOrEmpty(extractedText))
                {
                    await _qdrantService.IndexDocumentAsync(document.Document_Id, document.Document_FileName, extractedText, document.Meeting_Id);
                }

                var documentDto = new DocumentDto
                {
                    Document_Id = document.Document_Id,
                    Document_FileName = document.Document_FileName,
                    Document_FileType = document.Document_FileType,
                    Document_Description = document.Document_Description,
                    ExtractedText = document.ExtractedText,
                    FileSize = document.FileSize,
                    UploadedAt = document.UploadedAt,
                    Status = isTextExtracted ? "extrait" : "en_attente",
                    Preview = isTextExtracted && !string.IsNullOrEmpty(extractedText)
                        ? extractedText.Length > 100
                            ? extractedText.Substring(0, 100) + "..."
                            : extractedText
                        : null
                };

                return Ok(documentDto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur lors de l'extraction: {ex.Message}");
            }
        }

        // GET: api/documents/{id}/summary
        [HttpGet("{id}/summary")]
        public async Task<ActionResult<SummaryResponseDto>> GetDocumentSummary(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var document = await _context.Documents
                    .Include(d => d.Meeting)
                    .FirstOrDefaultAsync(d => d.Document_Id == id, cancellationToken);

                if (document == null)
                {
                    return NotFound("Document non trouvé.");
                }

                bool isAdmin = UserContextHelper.IsAdmin(User);
                var currentUserId = UserContextHelper.GetUserId(User);

                if (!isAdmin && currentUserId.HasValue && document.Meeting != null && document.Meeting.CreatedById.HasValue && document.Meeting.CreatedById.Value != currentUserId.Value)
                {
                    return StatusCode(403, "Accès refusé. Vous ne pouvez consulter que vos propres documents.");
                }

                if (!IsTextReallyExtracted(document.ExtractedText))
                {
                    return BadRequest("Le texte du document doit d'abord être extrait avant de pouvoir générer un résumé.");
                }

                var meetingId = document.Meeting_Id;
                if (!meetingId.HasValue || meetingId == Guid.Empty)
                {
                    var defaultMeeting = await _context.Meetings
                        .FirstOrDefaultAsync(m => m.Meeting_Title == "Documents sans meeting", cancellationToken);

                    if (defaultMeeting == null)
                    {
                        defaultMeeting = new Meeting
                        {
                            Meeting_Id = Guid.NewGuid(),
                            Meeting_Title = "Documents sans meeting",
                            MeetingDate = DateTime.UtcNow,
                            CreatedAt = DateTime.UtcNow,
                            CreatedById = currentUserId
                        };
                        _context.Meetings.Add(defaultMeeting);
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                    
                    meetingId = defaultMeeting.Meeting_Id;
                    document.Meeting_Id = meetingId;
                    await _context.SaveChangesAsync(cancellationToken);
                }

                var existingSummary = await _context.Aisummaries
                    .FirstOrDefaultAsync(s => s.Meeting_Id == meetingId.Value && s.Type == "summary", cancellationToken);

                if (existingSummary != null)
                {
                    return Ok(new SummaryResponseDto
                    {
                        Aisummary_Id = existingSummary.Aisummary_Id,
                        Document_Id = document.Document_Id,
                        Content = existingSummary.Content,
                        CreatedAt = existingSummary.Aisummary_CreatedAt,
                        Source = "cache"
                    });
                }

                var summaryText = await _ollamaService.GenerateSummaryAsync(document.ExtractedText!, cancellationToken);

                var newSummary = new Aisummary
                {
                    Aisummary_Id = Guid.NewGuid(),
                    Meeting_Id = meetingId.Value,
                    Type = "summary",
                    Content = summaryText,
                    Aisummary_CreatedAt = DateTime.UtcNow
                };

                _context.Aisummaries.Add(newSummary);
                await _context.SaveChangesAsync(cancellationToken);

                return Ok(new SummaryResponseDto
                {
                    Aisummary_Id = newSummary.Aisummary_Id,
                    Document_Id = document.Document_Id,
                    Content = newSummary.Content,
                    CreatedAt = newSummary.Aisummary_CreatedAt,
                    Source = "généré"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur lors de la génération du résumé: {ex.Message}");
            }
        }

        // GET: api/documents/search
        [HttpGet("search")]
        public async Task<ActionResult<DocumentResponseDto>> SearchDocuments([FromQuery] string? query)
        {
            bool isAdmin = UserContextHelper.IsAdmin(User);
            var currentUserId = UserContextHelper.GetUserId(User);

            var documentsQuery = _context.Documents
                .Include(d => d.Meeting)
                .AsQueryable();

            if (!isAdmin && currentUserId.HasValue)
            {
                documentsQuery = documentsQuery.Where(d => d.Meeting != null && d.Meeting.CreatedById == currentUserId.Value);
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                documentsQuery = documentsQuery.Where(d =>
                    d.Document_FileName.Contains(query) ||
                    (d.Document_Description != null && d.Document_Description.Contains(query)));
            }

            var documents = await documentsQuery
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();

            var documentDtos = documents.Select(d => new DocumentDto
            {
                Document_Id = d.Document_Id,
                Document_FileName = d.Document_FileName,
                Document_FileType = d.Document_FileType,
                Document_Description = d.Document_Description,
                ExtractedText = d.ExtractedText,
                FileSize = d.FileSize,
                UploadedAt = d.UploadedAt,
                Status = IsTextReallyExtracted(d.ExtractedText) ? "extrait" : "en_attente",
                Preview = IsTextReallyExtracted(d.ExtractedText) && !string.IsNullOrEmpty(d.ExtractedText)
                    ? d.ExtractedText.Length > 100
                        ? d.ExtractedText.Substring(0, 100) + "..."
                        : d.ExtractedText
                    : null
            }).ToList();

            return Ok(new DocumentResponseDto
            {
                Documents = documentDtos,
                TotalCount = documentDtos.Count,
                TotalSize = documentDtos.Sum(d => d.FileSize ?? 0),
                PendingCount = documentDtos.Count(d => d.Status == "en_attente")
            });
        }

        // DELETE: api/documents/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDocument(Guid id)
        {
            var document = await _context.Documents
                .Include(d => d.Meeting)
                .FirstOrDefaultAsync(d => d.Document_Id == id);

            if (document == null)
            {
                return NotFound();
            }

            bool isAdmin = UserContextHelper.IsAdmin(User);
            var currentUserId = UserContextHelper.GetUserId(User);

            if (!isAdmin && currentUserId.HasValue && document.Meeting != null && document.Meeting.CreatedById.HasValue && document.Meeting.CreatedById.Value != currentUserId.Value)
            {
                return StatusCode(403, "Accès refusé. Vous ne pouvez supprimer que vos propres documents.");
            }

            try
            {
                var filePath = Path.Combine(_environment.WebRootPath ?? "wwwroot", document.Document_FilePath);
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }
            catch
            {
                // Log l'erreur mais continue
            }

            try
            {
                await _qdrantService.DeleteDocumentVectorsAsync(id);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur suppression vecteurs Qdrant: {ex.Message}");
            }

            _context.Documents.Remove(document);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // POST: api/documents/{id}/ask
        [HttpPost("{id}/ask")]
        public async Task<ActionResult<AskQuestionResponseDto>> AskDocumentQuestion(Guid id, [FromBody] AskQuestionDto dto, CancellationToken cancellationToken)
        {
            var document = await _context.Documents
                .Include(d => d.Meeting)
                .FirstOrDefaultAsync(d => d.Document_Id == id, cancellationToken);

            if (document == null)
            {
                return NotFound("Document non trouvé.");
            }

            bool isAdmin = UserContextHelper.IsAdmin(User);
            var currentUserId = UserContextHelper.GetUserId(User);

            if (!isAdmin && currentUserId.HasValue && document.Meeting != null && document.Meeting.CreatedById.HasValue && document.Meeting.CreatedById.Value != currentUserId.Value)
            {
                return StatusCode(403, "Accès refusé. Vous ne pouvez interroger que vos propres documents.");
            }

            dto.DocumentId = id;
            return await AskQuestion(dto, cancellationToken);
        }

        // POST: api/documents/ask
        [HttpPost("ask")]
        public async Task<ActionResult<AskQuestionResponseDto>> AskQuestion([FromBody] AskQuestionDto dto, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(dto.Question))
            {
                return BadRequest("La question ne peut pas être vide.");
            }

            var sources = await _qdrantService.SearchSimilarChunksAsync(dto.Question, dto.Limit > 0 ? dto.Limit : 5, dto.DocumentId, dto.MeetingId, cancellationToken);
            var contextChunks = sources.Select(s => $"[Document: {s.DocumentName} (Score: {s.Score:P0})]\n{s.Content}").ToList();
            var answer = await _ollamaService.AnswerQuestionWithContextAsync(dto.Question, contextChunks, cancellationToken);

            return Ok(new AskQuestionResponseDto
            {
                Question = dto.Question,
                Answer = answer,
                DocumentId = dto.DocumentId,
                Sources = sources
            });
        }

        // ========== MÉTHODE UTILITAIRE ==========

        private bool IsTextReallyExtracted(string? extractedText)
        {
            if (string.IsNullOrEmpty(extractedText))
                return false;

            string[] exclusionPatterns = new string[]
            {
                "sera disponible",
                "Erreur",
                "non supporté",
                "Format",
                "Aucun contenu",
                "Fichier .pdf reçu",
                "Le fichier"
            };

            return !exclusionPatterns.Any(p => extractedText.Contains(p, StringComparison.OrdinalIgnoreCase));
        }
    }
}