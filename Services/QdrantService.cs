using AIDocumentMeetingAssistant.API.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace AIDocumentMeetingAssistant.API.Services
{
    public class QdrantService : IQdrantService
    {
        private readonly HttpClient _httpClient;
        private readonly IOllamaService _ollamaService;
        private readonly ILogger<QdrantService> _logger;
        private readonly string _collectionName;
        private readonly int _vectorSize;
        private readonly string _embeddingModel;

        public QdrantService(
            HttpClient httpClient,
            IOllamaService ollamaService,
            IConfiguration configuration,
            ILogger<QdrantService> logger)
        {
            _httpClient = httpClient;
            _ollamaService = ollamaService;
            _logger = logger;

            var baseUrl = configuration["Qdrant:BaseUrl"] ?? "http://localhost:6333";
            _httpClient.BaseAddress = new Uri(baseUrl.EndsWith("/") ? baseUrl : baseUrl + "/");
            _httpClient.Timeout = TimeSpan.FromMinutes(2);

            _collectionName = configuration["Qdrant:CollectionName"] ?? "documents";
            _vectorSize = int.TryParse(configuration["Qdrant:VectorSize"], out var size) ? size : 768;
            _embeddingModel = configuration["Qdrant:EmbeddingModel"] ?? "nomic-embed-text";
        }

        public async Task EnsureCollectionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var checkResponse = await _httpClient.GetAsync($"collections/{_collectionName}", cancellationToken);
                if (checkResponse.IsSuccessStatusCode)
                {
                    _logger.LogInformation("La collection Qdrant '{CollectionName}' existe déjà.", _collectionName);
                    return;
                }

                _logger.LogInformation("Création de la collection Qdrant '{CollectionName}' avec taille vectorielle {VectorSize}...", _collectionName, _vectorSize);

                var createPayload = new
                {
                    vectors = new
                    {
                        size = _vectorSize,
                        distance = "Cosine"
                    }
                };

                var createResponse = await _httpClient.PutAsJsonAsync($"collections/{_collectionName}", createPayload, cancellationToken);
                createResponse.EnsureSuccessStatusCode();

                _logger.LogInformation("Collection Qdrant '{CollectionName}' créée avec succès.", _collectionName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la vérification/création de la collection Qdrant.");
                throw;
            }
        }

        public async Task<bool> IndexDocumentAsync(Guid documentId, string fileName, string extractedText, Guid? meetingId = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                _logger.LogWarning("Indexation Qdrant annulée: Aucun texte extrait pour le document {DocumentId}.", documentId);
                return false;
            }

            try
            {
                await EnsureCollectionAsync(cancellationToken);

                // Supprimer d'abord les anciens points vectoriels pour ce document (pour ré-indexation propre)
                await DeleteDocumentVectorsAsync(documentId, cancellationToken);

                var chunks = ChunkText(extractedText, chunkSize: 700, overlap: 100);
                if (!chunks.Any())
                {
                    return false;
                }

                _logger.LogInformation("Indexation de {ChunkCount} fragments pour le document '{FileName}' ({DocumentId}) dans Qdrant...", chunks.Count, fileName, documentId);

                var points = new List<object>();

                for (int i = 0; i < chunks.Count; i++)
                {
                    var chunk = chunks[i];
                    var embedding = await _ollamaService.GenerateEmbeddingAsync(chunk, _embeddingModel, cancellationToken);
                    if (embedding == null || embedding.Length == 0)
                    {
                        _logger.LogWarning("Embedding vide généré pour le chunk {ChunkIndex} du document {DocumentId}.", i, documentId);
                        continue;
                    }

                    var pointId = Guid.NewGuid().ToString();
                    points.Add(new
                    {
                        id = pointId,
                        vector = embedding,
                        payload = new Dictionary<string, object?>
                        {
                            { "document_id", documentId.ToString() },
                            { "document_name", fileName },
                            { "meeting_id", meetingId?.ToString() },
                            { "chunk_index", i },
                            { "content", chunk }
                        }
                    });
                }

                if (!points.Any())
                {
                    _logger.LogWarning("Aucun point vectoriel à insérer dans Qdrant pour {DocumentId}.", documentId);
                    return false;
                }

                var upsertPayload = new { points = points };
                var upsertResponse = await _httpClient.PutAsJsonAsync($"collections/{_collectionName}/points?wait=true", upsertPayload, cancellationToken);
                upsertResponse.EnsureSuccessStatusCode();

                _logger.LogInformation("Indexation Qdrant réussie pour le document {DocumentId} ({PointCount} points).", documentId, points.Count);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'indexation du document {DocumentId} dans Qdrant.", documentId);
                return false;
            }
        }

        public async Task<List<QdrantSearchResultDto>> SearchSimilarChunksAsync(string query, int limit = 5, Guid? documentId = null, Guid? meetingId = null, CancellationToken cancellationToken = default)
        {
            var results = new List<QdrantSearchResultDto>();

            if (string.IsNullOrWhiteSpace(query))
            {
                return results;
            }

            try
            {
                await EnsureCollectionAsync(cancellationToken);

                var queryEmbedding = await _ollamaService.GenerateEmbeddingAsync(query, _embeddingModel, cancellationToken);
                if (queryEmbedding == null || queryEmbedding.Length == 0)
                {
                    _logger.LogWarning("Impossible de générer l'embedding pour la requête sémantique.");
                    return results;
                }

                var filterMust = new List<object>();
                if (documentId.HasValue && documentId.Value != Guid.Empty)
                {
                    filterMust.Add(new { key = "document_id", match = new { value = documentId.Value.ToString() } });
                }
                if (meetingId.HasValue && meetingId.Value != Guid.Empty)
                {
                    filterMust.Add(new { key = "meeting_id", match = new { value = meetingId.Value.ToString() } });
                }

                object searchPayload;
                if (filterMust.Any())
                {
                    searchPayload = new
                    {
                        vector = queryEmbedding,
                        limit = limit,
                        with_payload = true,
                        filter = new { must = filterMust }
                    };
                }
                else
                {
                    searchPayload = new
                    {
                        vector = queryEmbedding,
                        limit = limit,
                        with_payload = true
                    };
                }

                var searchResponse = await _httpClient.PostAsJsonAsync($"collections/{_collectionName}/points/search", searchPayload, cancellationToken);
                searchResponse.EnsureSuccessStatusCode();

                var jsonDoc = await searchResponse.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken);
                if (jsonDoc != null && jsonDoc.RootElement.TryGetProperty("result", out var resultElement))
                {
                    foreach (var item in resultElement.EnumerateArray())
                    {
                        var chunkId = item.GetProperty("id").GetString() ?? "";
                        var score = (float)item.GetProperty("score").GetDouble();
                        var payload = item.GetProperty("payload");

                        var docIdStr = payload.TryGetProperty("document_id", out var dProp) ? dProp.GetString() : null;
                        var docName = payload.TryGetProperty("document_name", out var dnProp) ? dnProp.GetString() ?? "" : "";
                        var meetIdStr = payload.TryGetProperty("meeting_id", out var mProp) ? mProp.GetString() : null;
                        var chunkIdx = payload.TryGetProperty("chunk_index", out var ciProp) ? ciProp.GetInt32() : 0;
                        var content = payload.TryGetProperty("content", out var cProp) ? cProp.GetString() ?? "" : "";

                        Guid.TryParse(docIdStr, out var parsedDocId);
                        Guid? parsedMeetId = Guid.TryParse(meetIdStr, out var g) ? g : null;

                        results.Add(new QdrantSearchResultDto
                        {
                            ChunkId = chunkId,
                            DocumentId = parsedDocId,
                            DocumentName = docName,
                            MeetingId = parsedMeetId,
                            ChunkIndex = chunkIdx,
                            Content = content,
                            Score = score
                        });
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la recherche sémantique dans Qdrant pour la requête '{Query}'.", query);
                return results;
            }
        }

        public async Task<bool> DeleteDocumentVectorsAsync(Guid documentId, CancellationToken cancellationToken = default)
        {
            try
            {
                var deletePayload = new
                {
                    filter = new
                    {
                        must = new[]
                        {
                            new { key = "document_id", match = new { value = documentId.ToString() } }
                        }
                    }
                };

                var response = await _httpClient.PostAsJsonAsync($"collections/{_collectionName}/points/delete?wait=true", deletePayload, cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la suppression des vecteurs Qdrant pour le document {DocumentId}.", documentId);
                return false;
            }
        }

        private List<string> ChunkText(string text, int chunkSize = 700, int overlap = 100)
        {
            var chunks = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return chunks;

            text = text.Replace("\r\n", "\n").Trim();
            if (text.Length <= chunkSize)
            {
                chunks.Add(text);
                return chunks;
            }

            int step = chunkSize - overlap;
            for (int i = 0; i < text.Length; i += step)
            {
                int length = Math.Min(chunkSize, text.Length - i);
                string chunk = text.Substring(i, length).Trim();
                if (!string.IsNullOrWhiteSpace(chunk))
                {
                    chunks.Add(chunk);
                }

                if (i + length >= text.Length)
                {
                    break;
                }
            }

            return chunks;
        }
    }
}
