using AIDocumentMeetingAssistant.API.DTOs;
using AIDocumentMeetingAssistant.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AIDocumentMeetingAssistant.API.Controllers
{
    /// <summary>
    /// Contrôleur dédié au Chatbot IA Intelligent & Multilingue Polia (Ollama + Qdrant RAG)
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class PoliaController : ControllerBase
    {
        private readonly IAIAgentService _agentService;
        private readonly IConfiguration _configuration;

        public PoliaController(IAIAgentService agentService, IConfiguration configuration)
        {
            _agentService = agentService;
            _configuration = configuration;
        }

        /// <summary>
        /// Poser une question à Polia en n'importe quelle langue (Français, Anglais, Arabe, Derja Tunisien)
        /// </summary>
        [HttpPost("chat")]
        public async Task<ActionResult<AgentChatResponseDto>> ChatWithPolia([FromBody] AgentChatRequestDto request, CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Question))
            {
                return BadRequest("La question à Polia ne peut pas être vide.");
            }

            try
            {
                var response = await _agentService.AskAgentAsync(request, cancellationToken);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur de traitement du Chatbot Polia: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtenir l'historique des conversations avec Polia
        /// </summary>
        [HttpGet("history")]
        public async Task<ActionResult<List<AgentHistoryDto>>> GetPoliaHistory([FromQuery] Guid? meetingId, CancellationToken cancellationToken)
        {
            try
            {
                var history = await _agentService.GetHistoryAsync(meetingId, cancellationToken);
                return Ok(history);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur lors de la récupération de l'historique de Polia: {ex.Message}");
            }
        }

        /// <summary>
        /// Supprimer un échange de l'historique
        /// </summary>
        [HttpDelete("history/{id}")]
        public async Task<IActionResult> DeleteHistoryItem(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                bool deleted = await _agentService.DeleteHistoryItemAsync(id, cancellationToken);
                if (!deleted)
                {
                    return NotFound("Élément d'historique non trouvé.");
                }
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur lors de la suppression de l'élément d'historique: {ex.Message}");
            }
        }

        /// <summary>
        /// Réindexer tous les documents et notes dans Qdrant pour la mémoire de Polia
        /// </summary>
        [HttpPost("index-all")]
        public async Task<IActionResult> IndexAllData(CancellationToken cancellationToken)
        {
            try
            {
                int totalIndexed = await _agentService.IndexAllApplicationDataAsync(cancellationToken);
                return Ok(new
                {
                    message = "Mémoire vectorielle de Polia mise à jour dans Qdrant avec succès.",
                    totalIndexed = totalIndexed
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur lors de l'indexation dans Qdrant: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtenir le statut des modèles et des connexions Ollama et Qdrant pour Polia
        /// </summary>
        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            return Ok(new
            {
                agentName = _configuration["Ollama:AgentName"] ?? "Polia",
                status = "Active & Ready",
                languagesSupported = new[] { "Français", "English", "العربية الفصحى", "Derja Tunisien (الدارجة التونسية)" },
                ollamaModel = _configuration["Ollama:Model"] ?? "qwen2.5",
                ollamaBaseUrl = _configuration["Ollama:BaseUrl"] ?? "http://localhost:11434",
                qdrantCollection = _configuration["Qdrant:CollectionName"] ?? "documents",
                qdrantBaseUrl = _configuration["Qdrant:BaseUrl"] ?? "http://localhost:6333"
            });
        }
    }
}
