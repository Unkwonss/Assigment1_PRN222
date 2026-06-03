using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace BusinessLayer.Services
{
    /// <summary>
    /// Singleton AI Engine - registered via AddSingleton in Program.cs
    /// Provides embedding generation, RAG response synthesis, and RAGAS scoring.
    /// </summary>
    public class SimulatedAIEngine
    {
        private readonly IConfiguration? _configuration;
        private static readonly HttpClient _httpClient = new HttpClient();

        // Injected via DI as Singleton (registered in Program.cs with AddSingleton<SimulatedAIEngine>)
        public SimulatedAIEngine(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // Generate a deterministic vector based on text content (size 384)
        public float[] GenerateEmbedding(string text, int dimensions = 384)
        {
            if (string.IsNullOrEmpty(text)) text = "empty";
            
            float[] vector = new float[dimensions];
            // Seed a random generator with the hash of the text so it's fully deterministic
            int seed = text.GetHashCode();
            Random rand = new Random(seed);

            float sumOfSquares = 0f;
            for (int i = 0; i < dimensions; i++)
            {
                vector[i] = (float)(rand.NextDouble() * 2.0 - 1.0);
                sumOfSquares += vector[i] * vector[i];
            }

            // L2 Normalize the vector so cosine similarity is just the dot product
            float magnitude = (float)Math.Sqrt(sumOfSquares);
            if (magnitude > 0)
            {
                for (int i = 0; i < dimensions; i++)
                {
                    vector[i] /= magnitude;
                }
            }

            return vector;
        }

        // Calculate Cosine Similarity between two L2 normalized vectors
        public double ComputeCosineSimilarity(float[] vecA, float[] vecB)
        {
            if (vecA == null || vecB == null || vecA.Length != vecB.Length) return 0;
            
            double dotProduct = 0;
            for (int i = 0; i < vecA.Length; i++)
            {
                dotProduct += vecA[i] * vecB[i];
            }
            return dotProduct;
        }

        // Generate rich RAG response with citations in Vietnamese
        public string GenerateRAGResponse(string userQuery, List<(int ChunkId, string Content, int? PageNumber)> contexts, string subjectCode)
        {
            // Try to call real APIs if configured (Gemini/Ollama)
            string? realResponse = CallRealAIIfNeededAsync(userQuery, contexts, subjectCode).GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(realResponse))
            {
                return realResponse;
            }

            if (contexts == null || !contexts.Any())
            {
                return $"[Guardrail] Xin chào! Tôi là Trợ lý học tập của môn học {subjectCode}. Xin lỗi, câu hỏi của bạn không thể được trả lời từ tài liệu hiện có trong hệ thống. Vui lòng cập nhật hoặc tải lên tài liệu học tập của chương học liên quan để tôi có thể hỗ trợ bạn chính xác nhất.";
            }

            // Quick keyword containment check to simulate guardrails
            var queryKeywords = ExtractKeywords(userQuery);
            bool hasOverlap = false;
            foreach (var ctx in contexts)
            {
                var contextKeywords = ExtractKeywords(ctx.Content);
                var intersection = queryKeywords.Intersect(contextKeywords, StringComparer.OrdinalIgnoreCase);
                if (intersection.Count() >= 1)
                {
                    hasOverlap = true;
                    break;
                }
            }

            // If there's absolutely no keyword overlap between query and context, refuse to answer
            if (!hasOverlap && queryKeywords.Count > 1)
            {
                return $"[Guardrail] Tôi rất tiếc, nhưng thông tin liên quan đến câu hỏi của bạn không được tìm thấy trong tài liệu học tập hiện tại của môn {subjectCode}. Để tuân thủ nguyên tắc chính xác và tránh nhầm lẫn, tôi chỉ trả lời dựa trên các tài liệu đã được tải lên.";
            }

            // Synthesize answer based on contexts
            StringBuilder answer = new StringBuilder();
            answer.AppendLine($"Dựa trên tài liệu học tập của môn **{subjectCode}**, tôi xin trả lời câu hỏi của bạn như sau:\n");

            // Divide context chunks to present synthesized points
            for (int i = 0; i < contexts.Count; i++)
            {
                var ctx = contexts[i];
                var summary = SummarizeChunk(ctx.Content, userQuery);
                int pageNum = ctx.PageNumber ?? 1;

                answer.AppendLine($"- **Ý {i + 1}:** {summary} [Nguồn {i + 1} - Trang {pageNum}].");
            }

            answer.AppendLine($"\nHẹn gặp lại bạn trong buổi học tiếp theo! Hãy hỏi tôi bất kỳ điều gì về tài liệu môn {subjectCode}.");
            return answer.ToString();
        }

        private async System.Threading.Tasks.Task<string?> CallRealAIIfNeededAsync(
            string userQuery, 
            List<(int ChunkId, string Content, int? PageNumber)> contexts, 
            string subjectCode)
        {
            if (_configuration == null) return null;

            string? geminiApiKey = _configuration["Gemini:ApiKey"];
            if (!string.IsNullOrEmpty(geminiApiKey) && geminiApiKey != "YOUR_GEMINI_API_KEY")
            {
                return await CallGeminiApiAsync(userQuery, contexts, subjectCode, geminiApiKey);
            }

            string? ollamaEnabled = _configuration["Ollama:Enabled"];
            if (ollamaEnabled == "true" || ollamaEnabled == "True")
            {
                string ollamaUrl = _configuration["Ollama:Url"] ?? "http://localhost:11434/api/generate";
                string ollamaModel = _configuration["Ollama:Model"] ?? "llama3";
                return await CallOllamaApiAsync(userQuery, contexts, subjectCode, ollamaUrl, ollamaModel);
            }

            return null;
        }

        private async System.Threading.Tasks.Task<string?> CallGeminiApiAsync(
            string userQuery, 
            List<(int ChunkId, string Content, int? PageNumber)> contexts, 
            string subjectCode, 
            string apiKey)
        {
            try
            {
                string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}";
                
                // Build a structured prompt with context
                var sbPrompt = new StringBuilder();
                sbPrompt.AppendLine($"Bạn là một Trợ lý giảng dạy AI thông minh và tận tâm cho môn học {subjectCode}.");
                sbPrompt.AppendLine("Nhiệm vụ của bạn là trả lời câu hỏi của sinh viên dựa trên các đoạn ngữ cảnh tài liệu học liệu được cung cấp dưới đây.");
                sbPrompt.AppendLine("Hãy trả lời bằng ngôn ngữ của câu hỏi (tiếng Việt, tiếng Nhật, tiếng Anh, v.v.) một cách tự nhiên, mạch lạc, khoa học.");
                sbPrompt.AppendLine("QUAN TRỌNG: Bạn CẦN trích dẫn rõ ràng xuất xứ thông tin từ các mảnh ngữ cảnh bằng cách thêm chỉ số [Nguồn số - Trang số] ở cuối các ý chính.");
                sbPrompt.AppendLine("Nếu câu hỏi không liên quan đến ngữ cảnh được cung cấp bên dưới, hãy lịch sự từ chối trả lời và nói rằng thông tin không nằm trong tài liệu học liệu hiện tại.");
                sbPrompt.AppendLine("\n--- BẮT ĐẦU NGỮ CẢNH TÀI LIỆU ---");
                
                int index = 1;
                foreach (var ctx in contexts)
                {
                    sbPrompt.AppendLine($"[Mảnh nguồn #{index} | Trang #{ctx.PageNumber ?? 1}]:");
                    sbPrompt.AppendLine(ctx.Content);
                    sbPrompt.AppendLine();
                    index++;
                }
                sbPrompt.AppendLine("--- KẾT THÚC NGỮ CẢNH TÀI LIỆU ---\n");
                sbPrompt.AppendLine($"Câu hỏi của sinh viên: {userQuery}");
                sbPrompt.AppendLine("Câu trả lời của bạn:");

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = sbPrompt.ToString() }
                            }
                        }
                    }
                };

                string jsonStr = System.Text.Json.JsonSerializer.Serialize(requestBody);
                using (var content = new StringContent(jsonStr, Encoding.UTF8, "application/json"))
                {
                    var response = await _httpClient.PostAsync(url, content);
                    if (response.IsSuccessStatusCode)
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();
                        using (var jsonDoc = System.Text.Json.JsonDocument.Parse(responseBody))
                        {
                            var root = jsonDoc.RootElement;
                            if (root.TryGetProperty("candidates", out var candidates) && 
                                candidates.ValueKind == System.Text.Json.JsonValueKind.Array && 
                                candidates.GetArrayLength() > 0)
                            {
                                var firstCandidate = candidates[0];
                                if (firstCandidate.TryGetProperty("content", out var contentElement) &&
                                    contentElement.TryGetProperty("parts", out var parts) &&
                                    parts.ValueKind == System.Text.Json.JsonValueKind.Array && 
                                    parts.GetArrayLength() > 0)
                                {
                                    return parts[0].GetProperty("text").GetString() ?? "";
                                }
                            }
                        }
                    }
                    else
                    {
                        string err = await response.Content.ReadAsStringAsync();
                        return $"[Lỗi Gemini API: {response.StatusCode} - {err}]";
                    }
                }
            }
            catch (Exception ex)
            {
                return $"[Lỗi kết nối Gemini API: {ex.Message}]";
            }
            return null;
        }

        private async System.Threading.Tasks.Task<string?> CallOllamaApiAsync(
            string userQuery, 
            List<(int ChunkId, string Content, int? PageNumber)> contexts, 
            string subjectCode, 
            string ollamaUrl, 
            string modelName)
        {
            try
            {
                var sbPrompt = new StringBuilder();
                sbPrompt.AppendLine($"Bạn là một Trợ lý giảng dạy AI cho môn học {subjectCode}.");
                sbPrompt.AppendLine("Nhiệm vụ của bạn là trả lời câu hỏi của sinh viên dựa trên ngữ cảnh sau:");
                sbPrompt.AppendLine("\n--- BẮT ĐẦU NGỮ CẢNH ---");
                int index = 1;
                foreach (var ctx in contexts)
                {
                    sbPrompt.AppendLine($"[Mảnh nguồn #{index} | Trang #{ctx.PageNumber ?? 1}]: {ctx.Content}");
                    index++;
                }
                sbPrompt.AppendLine("--- KẾT THÚC NGỮ CẢNH ---\n");
                sbPrompt.AppendLine($"Câu hỏi: {userQuery}");
                sbPrompt.AppendLine("Trả lời (hãy trích dẫn số nguồn và trang tương ứng):");

                var requestBody = new
                {
                    model = modelName,
                    prompt = sbPrompt.ToString(),
                    stream = false
                };

                string jsonStr = System.Text.Json.JsonSerializer.Serialize(requestBody);
                using (var content = new StringContent(jsonStr, Encoding.UTF8, "application/json"))
                {
                    var response = await _httpClient.PostAsync(ollamaUrl, content);
                    if (response.IsSuccessStatusCode)
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();
                        using (var jsonDoc = System.Text.Json.JsonDocument.Parse(responseBody))
                        {
                            var root = jsonDoc.RootElement;
                            if (root.TryGetProperty("response", out var responseProperty))
                            {
                                return responseProperty.GetString() ?? "";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return $"[Lỗi kết nối Ollama API: {ex.Message}]";
            }
            return null;
        }

        // Summarize a chunk briefly, highlighting details relevant to the query
        private string SummarizeChunk(string content, string query)
        {
            // Simplify clean up
            string clean = Regex.Replace(content, @"\s+", " ").Trim();
            if (clean.Length <= 120) return clean;

            // Try to find a sentence containing one of the query words
            var sentences = clean.Split(new[] { '.', '?', '!' }, StringSplitOptions.RemoveEmptyEntries);
            var queryWords = ExtractKeywords(query);

            foreach (var sentence in sentences)
            {
                var words = ExtractKeywords(sentence);
                if (words.Intersect(queryWords, StringComparer.OrdinalIgnoreCase).Any())
                {
                    string match = sentence.Trim();
                    if (match.Length > 20)
                    {
                        return match.Length > 150 ? match.Substring(0, 147) + "..." : match;
                    }
                }
            }

            // Fallback to substring
            return clean.Substring(0, 140) + "...";
        }

        // Extract meaningful keywords from Vietnamese text
        private HashSet<string> ExtractKeywords(string text)
        {
            if (string.IsNullOrEmpty(text)) return new HashSet<string>();
            
            // Remove punctuation, lowercase and split
            var cleanText = Regex.Replace(text.ToLower(), @"[^\w\s]", "");
            var words = cleanText.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            // Simple stopword filter (common Vietnamese grammar particles)
            var stopwords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "và", "của", "là", "các", "có", "trong", "được", "cho", "với", "những", "này", "để", "ra", "về", "thì", "tại"
            };

            return new HashSet<string>(words.Where(w => w.Length > 1 && !stopwords.Contains(w)), StringComparer.OrdinalIgnoreCase);
        }

        // Simulate RAGAS metric calculations
        public (double Faithfulness, double Relevance, double Precision, double Recall) CalculateRagasScores(
            string question, string groundTruth, string response, List<string> retrievedContexts)
        {
            // 1. Faithfulness (factual overlap between response and contexts)
            double faithfulness = 0.85; // default high baseline
            if (retrievedContexts != null && retrievedContexts.Any())
            {
                var responseWords = ExtractKeywords(response);
                var contextWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var ctx in retrievedContexts)
                {
                    foreach (var w in ExtractKeywords(ctx)) contextWords.Add(w);
                }

                if (responseWords.Any())
                {
                    var overlap = responseWords.Intersect(contextWords).Count();
                    faithfulness = Math.Min(1.0, 0.4 + 0.6 * ((double)overlap / responseWords.Count));
                }
            }

            // 2. Answer Relevance (how well response aligns with the question)
            double relevance = 0.90;
            var questionWords = ExtractKeywords(question);
            var responseKeywords = ExtractKeywords(response);
            if (questionWords.Any() && responseKeywords.Any())
            {
                var overlap = responseKeywords.Intersect(questionWords).Count();
                relevance = Math.Min(1.0, 0.5 + 0.5 * ((double)overlap / questionWords.Count));
            }

            // 3. Context Precision (how relevant the retrieved contexts are to the ground truth)
            double precision = 0.80;
            var groundTruthWords = ExtractKeywords(groundTruth);
            if (retrievedContexts != null && retrievedContexts.Any() && groundTruthWords.Any())
            {
                double sumPrecision = 0;
                for (int i = 0; i < retrievedContexts.Count; i++)
                {
                    var ctxWords = ExtractKeywords(retrievedContexts[i]);
                    var matchCount = ctxWords.Intersect(groundTruthWords).Count();
                    sumPrecision += matchCount > 0 ? (1.0 / (i + 1)) : 0;
                }
                precision = Math.Min(1.0, 0.4 + 0.6 * sumPrecision);
            }

            // 4. Context Recall (how much of the ground truth is captured in the retrieved contexts)
            double recall = 0.88;
            if (retrievedContexts != null && retrievedContexts.Any() && groundTruthWords.Any())
            {
                var allContextWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var ctx in retrievedContexts)
                {
                    foreach (var w in ExtractKeywords(ctx)) allContextWords.Add(w);
                }

                var recalledWords = groundTruthWords.Intersect(allContextWords).Count();
                recall = (double)recalledWords / groundTruthWords.Count;
            }

            // Cap the results at realistic boundaries and round to 2 decimals
            return (
                Math.Round(Math.Clamp(faithfulness, 0.1, 1.0), 2),
                Math.Round(Math.Clamp(relevance, 0.1, 1.0), 2),
                Math.Round(Math.Clamp(precision, 0.1, 1.0), 2),
                Math.Round(Math.Clamp(recall, 0.1, 1.0), 2)
            );
        }
    }
}
