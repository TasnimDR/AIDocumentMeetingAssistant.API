using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AIDocumentMeetingAssistant.API.Services
{
    public interface IOllamaService
    {
        Task<string> GenerateSummaryAsync(string text, CancellationToken cancellationToken = default);
        Task<string> GenerateMeetingMinutesAsync(string meetingTitle, string text, CancellationToken cancellationToken = default);
        Task<float[]> GenerateEmbeddingAsync(string text, string? model = null, CancellationToken cancellationToken = default);
        Task<string> AnswerQuestionWithContextAsync(string question, List<string> contextChunks, CancellationToken cancellationToken = default);
        Task<string> GenerateAgentResponseAsync(string question, List<string> contextChunks, CancellationToken cancellationToken = default);
    }
}


