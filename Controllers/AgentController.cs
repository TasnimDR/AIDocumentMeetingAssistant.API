using AIDocumentMeetingAssistant.API.DTOs;
using AIDocumentMeetingAssistant.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AIDocumentMeetingAssistant.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AgentController : ControllerBase
    {
        private readonly IAIAgentService _agentService;

        public AgentController(IAIAgentService agentService)
        {
            _agentService = agentService;
        }

        // POST: api/agent/chat
        [HttpPost("chat")]
        public async Task<ActionResult<AgentChatResponseDto>> ChatWithAgent([FromBody] AgentChatRequestDto request, CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Question))
            {
                return BadRequest("La question ne peut pas être vide.");
            }

            try
            {
                var response = await _agentService.AskAgentAsync(request, cancellationToken);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur de traitement de l'Agent IA: {ex.Message}");
            }
        }

        // GET: api/agent/history
        [HttpGet("history")]
        public async Task<ActionResult<List<AgentHistoryDto>>> GetAgentHistory([FromQuery] Guid? meetingId, CancellationToken cancellationToken)
        {
            try
            {
                var history = await _agentService.GetHistoryAsync(meetingId, cancellationToken);
                return Ok(history);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur lors de la récupération de l'historique: {ex.Message}");
            }
        }

        // DELETE: api/agent/history/{id}
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
                return StatusCode(500, $"Erreur lors de la suppression: {ex.Message}");
            }
        }

        // POST: api/agent/index-all
        [HttpPost("index-all")]
        public async Task<IActionResult> IndexAllData(CancellationToken cancellationToken)
        {
            try
            {
                int totalIndexed = await _agentService.IndexAllApplicationDataAsync(cancellationToken);
                return Ok(new
                {
                    message = "Indexation globale dans Qdrant réalisée avec succès.",
                    totalIndexed = totalIndexed
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur lors de l'indexation globale: {ex.Message}");
            }
        }
    }
}
