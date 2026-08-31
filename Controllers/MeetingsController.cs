using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AIDocumentMeetingAssistant.API.Models;
using AIDocumentMeetingAssistant.API.Services;
using AIDocumentMeetingAssistant.API.DTOs;
using AIDocumentMeetingAssistant.API.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.IO;

namespace AIDocumentMeetingAssistant.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MeetingsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IOllamaService _ollamaService;
        private readonly IWebHostEnvironment _environment;

        public MeetingsController(AppDbContext context, IOllamaService ollamaService, IWebHostEnvironment environment)
        {
            _context = context;
            _ollamaService = ollamaService;
            _environment = environment;
        }

        // GET: api/meetings
        [HttpGet]
        public async Task<ActionResult<IEnumerable<MeetingDto>>> GetMeetings()
        {
            bool isAdmin = UserContextHelper.IsAdmin(User);
            var currentUserId = UserContextHelper.GetUserId(User);

            var query = _context.Meetings
                .Include(m => m.CreatedBy)
                .AsQueryable();

            if (!isAdmin && currentUserId.HasValue)
            {
                query = query.Where(m => m.CreatedById == currentUserId.Value || m.CreatedById == null);
            }

            var meetings = await query
                .OrderByDescending(m => m.StartTime != default ? m.StartTime : m.MeetingDate)
                .ToListAsync();

            var meetingDtos = meetings.Select(m => {
                var start = m.StartTime != default ? m.StartTime : m.MeetingDate;
                var end = m.EndTime != default ? m.EndTime : start.AddHours(1);
                return new MeetingDto
                {
                    Meeting_Id = m.Meeting_Id,
                    Meeting_Title = m.Meeting_Title,
                    Participants = m.Participants,
                    MeetingDate = m.MeetingDate,
                    StartTime = start,
                    EndTime = end,
                    CreatedById = m.CreatedById,
                    CreatedByName = m.CreatedBy?.FullName,
                    CreatedAt = m.CreatedAt
                };
            }).ToList();

            return Ok(meetingDtos);
        }

        // GET: api/meetings/calendar
        [HttpGet("calendar")]
        public async Task<ActionResult<IEnumerable<CalendarMeetingDto>>> GetCalendarMeetings([FromQuery] DateTime? start, [FromQuery] DateTime? end)
        {
            bool isAdmin = UserContextHelper.IsAdmin(User);
            var currentUserId = UserContextHelper.GetUserId(User);

            var query = _context.Meetings
                .Include(m => m.CreatedBy)
                .AsQueryable();

            if (!isAdmin && currentUserId.HasValue)
            {
                query = query.Where(m => m.CreatedById == currentUserId.Value || m.CreatedById == null);
            }

            if (start.HasValue)
            {
                query = query.Where(m => (m.EndTime != default ? m.EndTime : m.MeetingDate.AddHours(1)) >= start.Value);
            }

            if (end.HasValue)
            {
                query = query.Where(m => (m.StartTime != default ? m.StartTime : m.MeetingDate) <= end.Value);
            }

            var meetings = await query.OrderBy(m => m.StartTime != default ? m.StartTime : m.MeetingDate).ToListAsync();

            var calendarDtos = meetings.Select(m => {
                var startTime = m.StartTime != default ? m.StartTime : m.MeetingDate;
                var endTime = m.EndTime != default ? m.EndTime : startTime.AddHours(1);
                return new CalendarMeetingDto
                {
                    Id = m.Meeting_Id,
                    Title = m.Meeting_Title,
                    Participants = m.Participants,
                    Start = startTime,
                    End = endTime,
                    CreatedById = m.CreatedById,
                    CreatedByName = m.CreatedBy?.FullName
                };
            }).ToList();

            return Ok(calendarDtos);
        }

        // GET: api/meetings/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<MeetingDto>> GetMeeting(Guid id)
        {
            var meeting = await _context.Meetings
                .Include(m => m.CreatedBy)
                .Include(m => m.MeetingNotes)
                .Include(m => m.Aisummaries)
                .Include(m => m.Documents)
                .FirstOrDefaultAsync(m => m.Meeting_Id == id);

            if (meeting == null)
            {
                return NotFound("Réunion non trouvée.");
            }

            bool isAdmin = UserContextHelper.IsAdmin(User);
            var currentUserId = UserContextHelper.GetUserId(User);

            if (!isAdmin && currentUserId.HasValue && meeting.CreatedById.HasValue && meeting.CreatedById.Value != currentUserId.Value)
            {
                return StatusCode(403, "Accès refusé. Vous ne pouvez consulter que vos propres réunions.");
            }

            var start = meeting.StartTime != default ? meeting.StartTime : meeting.MeetingDate;
            var end = meeting.EndTime != default ? meeting.EndTime : start.AddHours(1);

            var meetingDto = new MeetingDto
            {
                Meeting_Id = meeting.Meeting_Id,
                Meeting_Title = meeting.Meeting_Title,
                Participants = meeting.Participants,
                MeetingDate = meeting.MeetingDate,
                StartTime = start,
                EndTime = end,
                CreatedById = meeting.CreatedById,
                CreatedByName = meeting.CreatedBy?.FullName,
                CreatedAt = meeting.CreatedAt,
                MeetingNotes = meeting.MeetingNotes.Select(n => new MeetingNoteDto
                {
                    MeetingNote_Id = n.MeetingNote_Id,
                    NotesContent = n.NotesContent,
                    CreatedAt = n.CreatedAt
                }).OrderBy(n => n.CreatedAt).ToList(),
                Aisummaries = meeting.Aisummaries.Select(s => new AisummaryDto
                {
                    Aisummary_Id = s.Aisummary_Id,
                    Type = s.Type,
                    Content = s.Content,
                    CreatedAt = s.Aisummary_CreatedAt
                }).OrderByDescending(s => s.CreatedAt).ToList(),
                Documents = meeting.Documents.Select(d => new DocumentDto
                {
                    Document_Id = d.Document_Id,
                    Document_FileName = d.Document_FileName,
                    Document_FileType = d.Document_FileType,
                    Document_Description = d.Document_Description,
                    ExtractedText = d.ExtractedText,
                    FileSize = d.FileSize,
                    UploadedAt = d.UploadedAt,
                    Status = string.IsNullOrWhiteSpace(d.ExtractedText) ? "en_attente" : "extrait",
                    Preview = !string.IsNullOrEmpty(d.ExtractedText)
                        ? d.ExtractedText.Length > 100
                            ? d.ExtractedText.Substring(0, 100) + "..."
                            : d.ExtractedText
                        : null
                }).ToList()
            };

            return Ok(meetingDto);
        }

        // POST: api/meetings
        [HttpPost]
        public async Task<ActionResult<MeetingDto>> CreateMeeting(MeetingCreateDto createDto)
        {
            try
            {
                DateTime startTime = createDto.StartTime ?? createDto.MeetingDate;
                DateTime endTime = createDto.EndTime ?? startTime.AddHours(1);

                if (endTime <= startTime)
                {
                    return BadRequest("L'heure de fin de la réunion doit être postérieure à l'heure de début.");
                }

                var duration = (endTime - startTime).TotalMinutes;
                if (duration > 60)
                {
                    return BadRequest("La durée maximale d'une réunion ne peut pas dépasser 1 heure (60 minutes).");
                }

                var currentUserId = UserContextHelper.GetUserId(User);

                var meeting = new Meeting
                {
                    Meeting_Id = Guid.NewGuid(),
                    Meeting_Title = createDto.Meeting_Title,
                    Participants = createDto.Participants,
                    MeetingDate = startTime,
                    StartTime = startTime,
                    EndTime = endTime,
                    CreatedById = currentUserId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Meetings.Add(meeting);
                await _context.SaveChangesAsync();

                string? createdByName = null;
                if (currentUserId.HasValue)
                {
                    var creatorUser = await _context.Users.FindAsync(currentUserId.Value);
                    if (creatorUser != null)
                    {
                        createdByName = creatorUser.FullName;
                    }
                }

                var meetingDto = new MeetingDto
                {
                    Meeting_Id = meeting.Meeting_Id,
                    Meeting_Title = meeting.Meeting_Title,
                    Participants = meeting.Participants,
                    MeetingDate = meeting.MeetingDate,
                    StartTime = meeting.StartTime,
                    EndTime = meeting.EndTime,
                    CreatedById = meeting.CreatedById,
                    CreatedByName = createdByName,
                    CreatedAt = meeting.CreatedAt
                };

                return CreatedAtAction(nameof(GetMeeting), new { id = meeting.Meeting_Id }, meetingDto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur lors de la création de la réunion: {ex.Message}");
            }
        }

        // PUT: api/meetings/{id}
        [HttpPut("{id}")]
        public async Task<ActionResult<MeetingDto>> UpdateMeeting(Guid id, MeetingUpdateDto updateDto)
        {
            try
            {
                var meeting = await _context.Meetings
                    .Include(m => m.CreatedBy)
                    .FirstOrDefaultAsync(m => m.Meeting_Id == id);

                if (meeting == null)
                {
                    return NotFound("Réunion non trouvée.");
                }

                bool isAdmin = UserContextHelper.IsAdmin(User);
                var currentUserId = UserContextHelper.GetUserId(User);

                if (!isAdmin && currentUserId.HasValue && meeting.CreatedById.HasValue && meeting.CreatedById.Value != currentUserId.Value)
                {
                    return StatusCode(403, "Accès refusé. Vous ne pouvez modifier que vos propres réunions.");
                }

                if (string.IsNullOrWhiteSpace(updateDto.Meeting_Title))
                {
                    return BadRequest("Le titre de la réunion ne peut pas être vide.");
                }

                if (updateDto.EndTime <= updateDto.StartTime)
                {
                    return BadRequest("L'heure de fin de la réunion doit être postérieure à l'heure de début.");
                }

                var duration = (updateDto.EndTime - updateDto.StartTime).TotalMinutes;
                if (duration > 60)
                {
                    return BadRequest("La durée maximale d'une réunion ne peut pas dépasser 1 heure (60 minutes).");
                }

                meeting.Meeting_Title = updateDto.Meeting_Title;
                meeting.Participants = updateDto.Participants;
                meeting.StartTime = updateDto.StartTime;
                meeting.EndTime = updateDto.EndTime;
                meeting.MeetingDate = updateDto.StartTime;

                await _context.SaveChangesAsync();

                var meetingDto = new MeetingDto
                {
                    Meeting_Id = meeting.Meeting_Id,
                    Meeting_Title = meeting.Meeting_Title,
                    Participants = meeting.Participants,
                    MeetingDate = meeting.MeetingDate,
                    StartTime = meeting.StartTime,
                    EndTime = meeting.EndTime,
                    CreatedById = meeting.CreatedById,
                    CreatedByName = meeting.CreatedBy?.FullName,
                    CreatedAt = meeting.CreatedAt
                };

                return Ok(meetingDto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur lors de la mise à jour de la réunion: {ex.Message}");
            }
        }

        // POST: api/meetings/{id}/minutes
        [HttpPost("{id}/minutes")]
        public async Task<ActionResult<AisummaryDto>> GenerateMinutes(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var meeting = await _context.Meetings
                    .Include(m => m.MeetingNotes)
                    .Include(m => m.Documents)
                    .FirstOrDefaultAsync(m => m.Meeting_Id == id, cancellationToken);

                if (meeting == null)
                {
                    return NotFound("Réunion non trouvée.");
                }

                bool isAdmin = UserContextHelper.IsAdmin(User);
                var currentUserId = UserContextHelper.GetUserId(User);

                if (!isAdmin && currentUserId.HasValue && meeting.CreatedById.HasValue && meeting.CreatedById.Value != currentUserId.Value)
                {
                    return StatusCode(403, "Accès refusé. Vous ne pouvez générer un compte rendu que pour vos propres réunions.");
                }

                var notesText = string.Join("\n\n", meeting.MeetingNotes.Select(n => n.NotesContent));
                var docsText = string.Join("\n\n", meeting.Documents
                    .Where(d => !string.IsNullOrEmpty(d.ExtractedText))
                    .Select(d => d.ExtractedText));

                var sourceText = "";
                if (!string.IsNullOrWhiteSpace(notesText))
                {
                    sourceText += $"Notes de réunion :\n{notesText}\n\n";
                }
                if (!string.IsNullOrWhiteSpace(docsText))
                {
                    sourceText += $"Documents joints :\n{docsText}\n";
                }

                sourceText = sourceText.Trim();

                if (string.IsNullOrWhiteSpace(sourceText))
                {
                    return BadRequest("Aucune note ou document texte n'est associé à cette réunion pour générer le compte rendu.");
                }

                var minutesContent = await _ollamaService.GenerateMeetingMinutesAsync(meeting.Meeting_Title, sourceText, cancellationToken);

                var existingMinutes = await _context.Aisummaries
                    .FirstOrDefaultAsync(s => s.Meeting_Id == meeting.Meeting_Id && s.Type == "minutes", cancellationToken);

                if (existingMinutes != null)
                {
                    _context.Aisummaries.Remove(existingMinutes);
                }

                var newSummary = new Aisummary
                {
                    Aisummary_Id = Guid.NewGuid(),
                    Meeting_Id = meeting.Meeting_Id,
                    Type = "minutes",
                    Content = minutesContent,
                    Aisummary_CreatedAt = DateTime.UtcNow
                };

                _context.Aisummaries.Add(newSummary);
                await _context.SaveChangesAsync(cancellationToken);

                var dto = new AisummaryDto
                {
                    Aisummary_Id = newSummary.Aisummary_Id,
                    Type = newSummary.Type,
                    Content = newSummary.Content,
                    CreatedAt = newSummary.Aisummary_CreatedAt
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur lors de la génération du compte rendu IA: {ex.Message}");
            }
        }

        // POST: api/meetings/{id}/notes
        [HttpPost("{id}/notes")]
        public async Task<ActionResult<MeetingNoteDto>> AddNote(Guid id, MeetingNoteCreateDto createDto)
        {
            try
            {
                var meeting = await _context.Meetings.FindAsync(id);
                if (meeting == null)
                {
                    return NotFound("Réunion non trouvée.");
                }

                bool isAdmin = UserContextHelper.IsAdmin(User);
                var currentUserId = UserContextHelper.GetUserId(User);

                if (!isAdmin && currentUserId.HasValue && meeting.CreatedById.HasValue && meeting.CreatedById.Value != currentUserId.Value)
                {
                    return StatusCode(403, "Accès refusé. Vous ne pouvez ajouter des notes qu'à vos propres réunions.");
                }

                if (string.IsNullOrWhiteSpace(createDto.NotesContent))
                {
                    return BadRequest("Le contenu de la note ne peut pas être vide.");
                }

                var note = new MeetingNote
                {
                    MeetingNote_Id = Guid.NewGuid(),
                    Meeting_Id = id,
                    NotesContent = createDto.NotesContent,
                    CreatedAt = DateTime.UtcNow
                };

                _context.MeetingNotes.Add(note);
                await _context.SaveChangesAsync();

                var noteDto = new MeetingNoteDto
                {
                    MeetingNote_Id = note.MeetingNote_Id,
                    NotesContent = note.NotesContent,
                    CreatedAt = note.CreatedAt
                };

                return Ok(noteDto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur lors de l'ajout de la note: {ex.Message}");
            }
        }

        // DELETE: api/meetings/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMeeting(Guid id)
        {
            try
            {
                var meeting = await _context.Meetings
                    .Include(m => m.Documents)
                    .Include(m => m.MeetingNotes)
                    .Include(m => m.Aisummaries)
                    .Include(m => m.Questions)
                    .FirstOrDefaultAsync(m => m.Meeting_Id == id);

                if (meeting == null)
                {
                    return NotFound("Réunion non trouvée.");
                }

                bool isAdmin = UserContextHelper.IsAdmin(User);
                var currentUserId = UserContextHelper.GetUserId(User);

                if (!isAdmin && currentUserId.HasValue && meeting.CreatedById.HasValue && meeting.CreatedById.Value != currentUserId.Value)
                {
                    return StatusCode(403, "Accès refusé. Vous ne pouvez supprimer que vos propres réunions.");
                }

                foreach (var doc in meeting.Documents.ToList())
                {
                    try
                    {
                        var filePath = Path.Combine(_environment.WebRootPath ?? "wwwroot", doc.Document_FilePath);
                        if (System.IO.File.Exists(filePath))
                        {
                            System.IO.File.Delete(filePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[MeetingsController] Erreur suppression fichier physique document {doc.Document_Id}: {ex.Message}");
                    }
                    _context.Documents.Remove(doc);
                }

                foreach (var note in meeting.MeetingNotes.ToList())
                {
                    _context.MeetingNotes.Remove(note);
                }

                foreach (var summary in meeting.Aisummaries.ToList())
                {
                    _context.Aisummaries.Remove(summary);
                }

                foreach (var question in meeting.Questions.ToList())
                {
                    var answer = await _context.Answers.FirstOrDefaultAsync(a => a.Question_Id == question.Question_Id);
                    if (answer != null)
                    {
                        _context.Answers.Remove(answer);
                    }
                    _context.Questions.Remove(question);
                }

                _context.Meetings.Remove(meeting);

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur lors de la suppression de la réunion: {ex.Message}");
            }
        }
    }
}
