using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace AIDocumentMeetingAssistant.API.Services
{
    public class OllamaService : IOllamaService
    {
        private readonly HttpClient _httpClient;
        private readonly string _modelName;
        private readonly string _embeddingModelName;
        private readonly string _agentName;

        public OllamaService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            
            var baseUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
            _httpClient.BaseAddress = new Uri(baseUrl.EndsWith("/") ? baseUrl : baseUrl + "/");
            _httpClient.Timeout = TimeSpan.FromMinutes(3); // Timeout de 3 minutes max

            _modelName = configuration["Ollama:Model"] ?? "qwen2.5";
            _embeddingModelName = configuration["Ollama:EmbeddingModel"] ?? "nomic-embed-text";
            _agentName = configuration["Ollama:AgentName"] ?? "Polia";
        }

        public async Task<float[]> GenerateEmbeddingAsync(string text, string? model = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<float>();
            }

            var requestPayload = new OllamaEmbeddingRequest
            {
                Model = string.IsNullOrWhiteSpace(model) ? _embeddingModelName : model,
                Prompt = text
            };

            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/embeddings", requestPayload, cancellationToken);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(cancellationToken: cancellationToken);
                if (result?.Embedding == null || result.Embedding.Length == 0)
                {
                    throw new Exception("L'embedding retourné par Ollama est vide.");
                }

                return result.Embedding;
            }
            catch (Exception ex)
            {
                throw new Exception($"Erreur lors de la génération de l'embedding Ollama: {ex.Message}", ex);
            }
        }

        public async Task<string> AnswerQuestionWithContextAsync(string question, List<string> contextChunks, CancellationToken cancellationToken = default)
        {
            return await GenerateAgentResponseAsync(question, contextChunks, cancellationToken);
        }

        public async Task<string> GenerateAgentResponseAsync(string question, List<string> contextChunks, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(question))
            {
                return "Veuillez poser une question valide à Polia.";
            }

            string systemPrompt =
                $"Tu es {_agentName}, l'assistante virtuelle IA dédiée exclusivement à l'application d'assistance aux réunions et d'analyse documentaire (AI Document & Meeting Assistant).\n\n" +
                "RÈGLES STRICTES DE COMPORTEMENT, DE LANGUE ET D'INTELLIGENCE :\n" +
                "1. SPÉCIALISATION APPLICATIVE & GUARDRAIL HORS-SUJET :\n" +
                "   - Tu es STRICTEMENT spécialisée dans l'aide aux réunions, l'analyse de documents, la recherche de comptes-rendus et les fonctionnalités de l'application.\n" +
                "   - Si l'utilisateur pose une question HORS-SUJET applicatif (ex: recette de pizza, cuisine, sport, jeux vidéo, météo générale), réponds poliment DANS LA LANGUE DE L'UTILISATEUR que tu es une assistante dédiée aux réunions et documents et invite-le à poser une question sur ses réunions ou fichiers.\n" +
                "2. RÈGLE ABSOLUE DE CORRESPONDANCE DE LANGUE :\n" +
                "   - Tu dois TOUJOURS répondre EXACTEMENT dans la MÊME LANGUE que la question posée par l'utilisateur !\n" +
                "   - Question en FRANÇAIS -> Réponse 100% en FRANÇAIS.\n" +
                "   - Question en ANGLAIS -> Réponse 100% en ANGLAIS.\n" +
                "   - Question en ARABE LITTÉRAIRE (الفصحى) -> Réponse 100% en ARABE LITTÉRAIRE.\n" +
                "   - Question en DERJA TUNISIEN (ex: 'chneya a7walek', 'عيشك', 'شني أحوالك', 'شنوا') -> Réponse 100% en DERJA TUNISIEN.\n" +
                "3. ACCURATISME & ZERO HALLUCINATION (REUNIONS & PARTICIPANTS) :\n" +
                "   - Basa-toi STRICTEMENT sur les données fournies dans le contexte ci-dessous (Base de données SQL et Qdrant).\n" +
                "   - Si l'utilisateur demande des informations sur une réunion (ex: 'participants de la Réunion 2') et que les données du contexte sont absentes, indique poliment dans sa langue que cette réunion ou ces informations ne figurent pas dans la base de données de l'application.\n" +
                "   - N'INVENTE JAMAIS de faux noms de participants ou de détails inexistants !\n" +
                "4. INTERDICTION DU CHINOIS : N'écris JAMAIS aucun caractère chinois ou symbole asiatique.\n" +
                "5. CONCISION : Rédige des réponses claires, précises et structurées.";

            string userContent = contextChunks != null && contextChunks.Count > 0
                ? $"--- CONTEXTE DOCUMENTAIRE (QDRANT) ---\n{string.Join("\n\n---\n\n", contextChunks)}\n------------------------------------\n\nQuestion de l'utilisateur : {question}"
                : $"Question de l'utilisateur : {question}";

            var requestPayload = new OllamaChatRequest
            {
                Model = _modelName,
                Messages = new List<OllamaChatMessage>
                {
                    new OllamaChatMessage { Role = "system", Content = systemPrompt },
                    new OllamaChatMessage { Role = "user", Content = userContent }
                },
                Options = new OllamaOptions
                {
                    Temperature = 0.2f, // Basse température pour éviter le vagabondage de langue et les hallucinations
                    NumPredict = 512,  // Réponse concise pour une génération très rapide
                    NumCtx = 4096
                },
                Stream = false
            };

            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/chat", requestPayload, cancellationToken);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken: cancellationToken);
                
                if (result?.Message == null || string.IsNullOrWhiteSpace(result.Message.Content))
                {
                    throw new Exception("La réponse du chatbot Polia via Ollama est vide.");
                }

                return result.Message.Content.Trim();
            }
            catch (Exception ex)
            {
                throw new Exception($"Erreur de communication avec Polia (Ollama api/chat): {ex.Message}", ex);
            }
        }

        public async Task<string> GenerateSummaryAsync(string text, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "Le document ne contient aucun texte à résumer.";
            }

            const int maxCharacters = 100000;
            if (text.Length > maxCharacters)
            {
                text = text.Substring(0, maxCharacters) + "... [Texte tronqué pour le résumé]";
            }

            var systemPrompt = "Tu es un expert en synthèse documentaire. Rédige un résumé exécutif clair et structuré en français.";
            var userContent = $"Résume le document suivant de façon professionnelle avec :\n- Résumé exécutif\n- Sujets principaux\n- Décisions clés\n- Risques & Recommandations\n\nDocument:\n{text}";

            var requestPayload = new OllamaChatRequest
            {
                Model = _modelName,
                Messages = new List<OllamaChatMessage>
                {
                    new OllamaChatMessage { Role = "system", Content = systemPrompt },
                    new OllamaChatMessage { Role = "user", Content = userContent }
                },
                Options = new OllamaOptions { Temperature = 0.2f, NumPredict = 768 },
                Stream = false
            };

            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/chat", requestPayload, cancellationToken);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken: cancellationToken);
                
                if (result?.Message == null || string.IsNullOrWhiteSpace(result.Message.Content))
                {
                    throw new Exception("La réponse d'Ollama est vide.");
                }

                return result.Message.Content.Trim();
            }
            catch (Exception ex)
            {
                throw new Exception($"Erreur lors de la communication avec Ollama: {ex.Message}", ex);
            }
        }

        public async Task<string> GenerateMeetingMinutesAsync(string meetingTitle, string text, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "Aucune note ou document n'est disponible pour ce meeting pour générer le compte rendu.";
            }

            const int maxCharacters = 100000;
            if (text.Length > maxCharacters)
            {
                text = text.Substring(0, maxCharacters) + "... [Texte tronqué pour le compte rendu]";
            }

            var systemPrompt = "Tu es un assistant de réunion professionnel. Génère un compte-rendu de réunion structuré et précis en français.";
            var userContent = $"Génère le compte-rendu de réunion pour '{meetingTitle}' avec :\n- Objectif de la réunion\n- Résumé des discussions\n- Décisions prises\n- Plan d'actions (Tâche, Responsable, Échéance)\n\nContenu de la réunion:\n{text}";

            var requestPayload = new OllamaChatRequest
            {
                Model = _modelName,
                Messages = new List<OllamaChatMessage>
                {
                    new OllamaChatMessage { Role = "system", Content = systemPrompt },
                    new OllamaChatMessage { Role = "user", Content = userContent }
                },
                Options = new OllamaOptions { Temperature = 0.2f, NumPredict = 768 },
                Stream = false
            };

            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/chat", requestPayload, cancellationToken);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken: cancellationToken);
                
                if (result?.Message == null || string.IsNullOrWhiteSpace(result.Message.Content))
                {
                    throw new Exception("La réponse d'Ollama est vide.");
                }

                return result.Message.Content.Trim();
            }
            catch (Exception ex)
            {
                throw new Exception($"Erreur lors de la communication avec Ollama: {ex.Message}", ex);
            }
        }

        private class OllamaEmbeddingRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; set; } = null!;

            [JsonPropertyName("prompt")]
            public string Prompt { get; set; } = null!;
        }

        private class OllamaEmbeddingResponse
        {
            [JsonPropertyName("embedding")]
            public float[] Embedding { get; set; } = Array.Empty<float>();
        }

        private class OllamaChatRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; set; } = null!;

            [JsonPropertyName("messages")]
            public List<OllamaChatMessage> Messages { get; set; } = new();

            [JsonPropertyName("options")]
            public OllamaOptions? Options { get; set; }

            [JsonPropertyName("stream")]
            public bool Stream { get; set; } = false;
        }

        private class OllamaChatMessage
        {
            [JsonPropertyName("role")]
            public string Role { get; set; } = null!;

            [JsonPropertyName("content")]
            public string Content { get; set; } = null!;
        }

        private class OllamaOptions
        {
            [JsonPropertyName("temperature")]
            public float Temperature { get; set; } = 0.2f;

            [JsonPropertyName("num_predict")]
            public int NumPredict { get; set; } = 512;

            [JsonPropertyName("num_ctx")]
            public int NumCtx { get; set; } = 4096;
        }

        private class OllamaChatResponse
        {
            [JsonPropertyName("model")]
            public string Model { get; set; } = null!;

            [JsonPropertyName("message")]
            public OllamaChatMessage? Message { get; set; }
        }
    }
}
