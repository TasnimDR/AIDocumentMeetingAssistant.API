using AIDocumentMeetingAssistant.API.DTOs;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AIDocumentMeetingAssistant.API.Services
{
    public interface IQdrantService
    {
        Task EnsureCollectionAsync(CancellationToken cancellationToken = default);
        Task<bool> IndexDocumentAsync(Guid documentId, string fileName, string extractedText, Guid? meetingId = null, CancellationToken cancellationToken = default);
        Task<List<QdrantSearchResultDto>> SearchSimilarChunksAsync(string query, int limit = 5, Guid? documentId = null, Guid? meetingId = null, CancellationToken cancellationToken = default);
        Task<bool> DeleteDocumentVectorsAsync(Guid documentId, CancellationToken cancellationToken = default);
    }
}
