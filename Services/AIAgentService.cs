using AIDocumentMeetingAssistant.API.DTOs;
using AIDocumentMeetingAssistant.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIDocumentMeetingAssistant.API.Services
{
    public class AIAgentService : IAIAgentService
    {
        private readonly AppDbContext _context;
        private readonly IOllamaService _ollamaService;
        private readonly IQdrantService _qdrantService;
        private readonly ILogger<AIAgentService> _logger;

        public AIAgentService(
            AppDbContext context,
            IOllamaService ollamaService,
            IQdrantService qdrantService,
            ILogger<AIAgentService> logger)
        {
            _context = context;
            _ollamaService = ollamaService;
            _qdrantService = qdrantService;
            _logger = logger;
        }

        public async Task<AgentChatResponseDto> AskAgentAsync(AgentChatRequestDto request, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.Question))
            {
                throw new ArgumentException("La question de l'Agent IA ne peut pas être vide.");
            }

            _logger.LogInformation("L'Agent IA traite la question: '{Question}'", request.Question);

            var contextChunks = new List<string>();

            // 0. Recherche directe dans la base SQL pour les réunions (titre, participants, organisateur, date)
            var allMeetings = await _context.Meetings
                .Include(m => m.CreatedBy)
                .Include(m => m.MeetingNotes)
                .Include(m => m.Documents)
                .ToListAsync(cancellationToken);

            string questionLower = request.Question.ToLowerInvariant();

            foreach (var m in allMeetings)
            {
                bool titleMatch = m.Meeting_Title != null && (
                    questionLower.Contains(m.Meeting_Title.ToLowerInvariant()) ||
                    questionLower.Replace(" ", "").Contains(m.Meeting_Title.ToLowerInvariant().Replace(" ", ""))
                );
                bool idMatch = request.MeetingId.HasValue && request.MeetingId.Value == m.Meeting_Id;

                // Si la question mentionne cette réunion ou est une question sur les réunions
                if (titleMatch || idMatch || questionLower.Contains("réunion") || questionLower.Contains("reunion") || questionLower.Contains("meeting") || questionLower.Contains("participant"))
                {
                    string organizer = m.CreatedBy?.FullName ?? m.CreatedBy?.UserName ?? m.CreatedBy?.Email ?? "Moi (Organisateur)";
                    string participantsStr = !string.IsNullOrWhiteSpace(m.Participants) ? m.Participants : "Aucun participant spécifié";
                    string notesStr = m.MeetingNotes.Any()
                        ? string.Join("; ", m.MeetingNotes.Select(n => n.NotesContent))
                        : "Aucune note";
                    string docsStr = m.Documents.Any()
                        ? string.Join(", ", m.Documents.Select(d => d.Document_FileName))
                        : "Aucun document";

                    string meetingInfo = $"[Base de données SQL - Données réelles de la Réunion : '{m.Meeting_Title}']\n" +
                                         $"- Identifiant Réunion : {m.Meeting_Id}\n" +
                                         $"- Date : {m.MeetingDate:dd/MM/yyyy HH:mm}\n" +
                                         $"- Organisateur : {organizer}\n" +
                                         $"- Participants Officiels : {participantsStr}\n" +
                                         $"- Documents joints : {docsStr}\n" +
                                         $"- Notes & Résumés : {notesStr}";

                    contextChunks.Add(meetingInfo);
                }
            }

            // 1. Recherche sémantique dans Qdrant pour obtenir le contexte vectoriel documentaire
            int limit = request.Limit > 0 ? request.Limit : 5;
            var searchResults = await _qdrantService.SearchSimilarChunksAsync(
                request.Question,
                limit,
                request.DocumentId,
                request.MeetingId,
                cancellationToken
            );

            var relevantSources = searchResults
                .Where(s => s.Score >= 0.30f)
                .OrderByDescending(s => s.Score)
                .ToList();

            if (!relevantSources.Any() && searchResults.Any())
            {
                relevantSources = searchResults.Take(2).ToList();
            }

            foreach (var s in relevantSources)
            {
                contextChunks.Add($"[Recherche Vectorielle Qdrant - Source: {s.DocumentName} (Pertinence: {s.Score:P0})]\n{s.Content}");
            }

            // 2. Demander la réponse à Polia via Ollama avec prompt spécialisé et zéro hallucination
            string agentAnswer = await _ollamaService.GenerateAgentResponseAsync(
                request.Question,
                contextChunks,
                cancellationToken
            );

            // 3. Sauvegarde dans SQL Server
            Guid meetingId = request.MeetingId ?? Guid.Empty;
            if (meetingId == Guid.Empty)
            {
                var defaultMeeting = await _context.Meetings
                    .FirstOrDefaultAsync(m => m.Meeting_Title == "Polia AI Conversation" || m.Meeting_Title == "Agent IA Conversation", cancellationToken);

                if (defaultMeeting == null)
                {
                    defaultMeeting = new Meeting
                    {
                        Meeting_Id = Guid.NewGuid(),
                        Meeting_Title = "Polia AI Conversation",
                        MeetingDate = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        CreatedById = null
                    };
                    _context.Meetings.Add(defaultMeeting);
                    await _context.SaveChangesAsync(cancellationToken);
                }

                meetingId = defaultMeeting.Meeting_Id;
            }

            var questionEntity = new Question
            {
                Question_Id = Guid.NewGuid(),
                Meeting_Id = meetingId,
                Question_Content = request.Question,
                CreatedAt = DateTime.UtcNow
            };

            var answerEntity = new Answer
            {
                Answer_Id = Guid.NewGuid(),
                Question_Id = questionEntity.Question_Id,
                Answer_Content = agentAnswer,
                CreatedAt = DateTime.UtcNow
            };

            _context.Questions.Add(questionEntity);
            _context.Answers.Add(answerEntity);
            await _context.SaveChangesAsync(cancellationToken);

            return new AgentChatResponseDto
            {
                QuestionId = questionEntity.Question_Id,
                AnswerId = answerEntity.Answer_Id,
                Question = questionEntity.Question_Content,
                Answer = answerEntity.Answer_Content,
                MeetingId = request.MeetingId,
                Sources = relevantSources,
                CreatedAt = questionEntity.CreatedAt
            };
        }

        public async Task<List<AgentHistoryDto>> GetHistoryAsync(Guid? meetingId = null, CancellationToken cancellationToken = default)
        {
            var query = _context.Questions
                .Include(q => q.Answer)
                .AsQueryable();

            if (meetingId.HasValue && meetingId.Value != Guid.Empty)
            {
                query = query.Where(q => q.Meeting_Id == meetingId.Value);
            }

            var questions = await query
                .OrderByDescending(q => q.CreatedAt)
                .ToListAsync(cancellationToken);

            return questions.Select(q => new AgentHistoryDto
            {
                QuestionId = q.Question_Id,
                AnswerId = q.Answer?.Answer_Id ?? Guid.Empty,
                MeetingId = q.Meeting_Id,
                Question = q.Question_Content,
                Answer = q.Answer?.Answer_Content ?? "Pas de réponse enregistrée.",
                CreatedAt = q.CreatedAt
            }).ToList();
        }

        public async Task<bool> DeleteHistoryItemAsync(Guid questionId, CancellationToken cancellationToken = default)
        {
            var question = await _context.Questions
                .Include(q => q.Answer)
                .FirstOrDefaultAsync(q => q.Question_Id == questionId, cancellationToken);

            if (question == null)
            {
                return false;
            }

            if (question.Answer != null)
            {
                _context.Answers.Remove(question.Answer);
            }

            _context.Questions.Remove(question);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<int> IndexAllApplicationDataAsync(CancellationToken cancellationToken = default)
        {
            int totalIndexed = 0;

            // 1. Indexer tous les documents ayant du texte extrait
            var documents = await _context.Documents
                .Where(d => d.ExtractedText != null && d.ExtractedText != "")
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Indexation globale: {Count} documents trouvés à indexer...", documents.Count);

            foreach (var doc in documents)
            {
                bool success = await _qdrantService.IndexDocumentAsync(
                    doc.Document_Id,
                    doc.Document_FileName,
                    doc.ExtractedText!,
                    doc.Meeting_Id,
                    cancellationToken
                );
                if (success) totalIndexed++;
            }

            // 2. Indexer toutes les notes de réunion
            var meetingNotes = await _context.MeetingNotes
                .Include(n => n.Meeting)
                .Where(n => n.NotesContent != null && n.NotesContent != "")
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Indexation globale: {Count} notes de réunion trouvées à indexer...", meetingNotes.Count);

            foreach (var note in meetingNotes)
            {
                string title = note.Meeting?.Meeting_Title ?? "Compte-rendu de réunion";
                bool success = await _qdrantService.IndexDocumentAsync(
                    note.MeetingNote_Id,
                    $"Note_{title}.txt",
                    note.NotesContent,
                    note.Meeting_Id,
                    cancellationToken
                );
                if (success) totalIndexed++;
            }

            // 3. Indexer toutes les réunions (Titre, Date, Participants, Organisateur)
            var meetings = await _context.Meetings
                .Include(m => m.CreatedBy)
                .Include(m => m.MeetingNotes)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Indexation globale: {Count} réunions trouvées à indexer...", meetings.Count);

            foreach (var meeting in meetings)
            {
                string organizer = meeting.CreatedBy?.FullName ?? meeting.CreatedBy?.UserName ?? "Moi";
                string participantsStr = !string.IsNullOrWhiteSpace(meeting.Participants) ? meeting.Participants : "Aucun participant";
                string notesStr = meeting.MeetingNotes.Any()
                    ? string.Join("\n", meeting.MeetingNotes.Select(n => n.NotesContent))
                    : "Pas de notes.";

                string meetingText = $"Réunion : {meeting.Meeting_Title}\n" +
                                     $"Organisateur : {organizer}\n" +
                                     $"Participants : {participantsStr}\n" +
                                     $"Date : {meeting.MeetingDate:dd/MM/yyyy HH:mm}\n" +
                                     $"Notes : {notesStr}";

                bool success = await _qdrantService.IndexDocumentAsync(
                    meeting.Meeting_Id,
                    $"Meeting_{meeting.Meeting_Title}.txt",
                    meetingText,
                    meeting.Meeting_Id,
                    cancellationToken
                );
                if (success) totalIndexed++;
            }

            _logger.LogInformation("Indexation globale terminée: {TotalIndexed} éléments indexés dans Qdrant.", totalIndexed);
            return totalIndexed;
        }
    }
}
