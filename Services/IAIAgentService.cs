using AIDocumentMeetingAssistant.API.DTOs;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AIDocumentMeetingAssistant.API.Services
{
    public interface IAIAgentService
    {
        Task<AgentChatResponseDto> AskAgentAsync(AgentChatRequestDto request, CancellationToken cancellationToken = default);
        Task<List<AgentHistoryDto>> GetHistoryAsync(Guid? meetingId = null, CancellationToken cancellationToken = default);
        Task<bool> DeleteHistoryItemAsync(Guid questionId, CancellationToken cancellationToken = default);
        Task<int> IndexAllApplicationDataAsync(CancellationToken cancellationToken = default);
    }
}
