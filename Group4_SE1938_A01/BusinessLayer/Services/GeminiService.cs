using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BusinessLayer.Interfaces;
using BusinessLayer.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessLayer.Services
{
    public class GeminiService : IGeminiService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly GeminiSettings _settings;
        private readonly ILogger<GeminiService> _logger;

        public GeminiService(
            IHttpClientFactory httpClientFactory,
            IOptions<GeminiSettings> settings,
            ILogger<GeminiService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<string> GenerateResponseAsync(
            string userQuestion,
            List<string> contextChunks,
            List<(string role, string content)> conversationHistory,
            string subjectName = "")
        {
            try
            {
                var safeChunks = contextChunks ?? new List<string>();
                var safeHistory = conversationHistory ?? new List<(string, string)>();
                var safeSubject = subjectName ?? string.Empty;

                _logger.LogInformation(
                    "[RAG] GenerateResponseAsync called. Chunks: {Count}, Question: {Question}",
                    safeChunks.Count,
                    userQuestion.Length > 100 ? userQuestion[..100] : userQuestion);

                if (safeChunks.Count > 0)
                {
                    _logger.LogInformation(
                        "[RAG] Chunk 1 preview (100 chars): {Preview}",
                        safeChunks[0].Length > 100 ? safeChunks[0][..100] : safeChunks[0]);
                }
                else
                {
                    _logger.LogWarning("[RAG] contextChunks is EMPTY — Gemini will have no context!");
                }

                var prompt = BuildRAGPrompt(userQuestion, safeChunks, safeHistory, safeSubject);

                _logger.LogInformation(
                    "[RAG] Prompt length: {Length} chars. First 500 chars: {Prompt}",
                    prompt.Length,
                    prompt.Length > 500 ? prompt[..500] : prompt);

                var response = await CallGeminiAPIAsync(prompt);

                _logger.LogInformation(
                    "[RAG] Gemini response (first 300 chars): {Response}",
                    response.Length > 300 ? response[..300] : response);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gọi Gemini API: {Message}", ex.Message);
                return "Xin lỗi, đã có lỗi xảy ra khi xử lý câu hỏi của bạn.";
            }
        }

        private string BuildRAGPrompt(
            string question,
            List<string> chunks,
            List<(string role, string content)> history,
            string subject)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Bạn là trợ lý học tập cho môn " + (string.IsNullOrEmpty(subject) ? "học" : subject) + ".");
            sb.AppendLine("Nhiệm vụ: Đọc kỹ TÀI LIỆU THAM KHẢO bên dưới, sau đó trả lời câu hỏi của sinh viên dựa trên nội dung tài liệu đó.");
            sb.AppendLine("Hãy trích dẫn và giải thích nội dung từ tài liệu một cách chi tiết.");
            sb.AppendLine("CHỈ khi tài liệu hoàn toàn KHÔNG chứa bất kỳ thông tin nào liên quan đến câu hỏi, hãy trả lời: 'Tài liệu chưa đề cập nội dung này.'");
            sb.AppendLine("Trả lời bằng tiếng Việt, rõ ràng, có cấu trúc.");
            sb.AppendLine();

            if (chunks != null && chunks.Count > 0)
            {
                sb.AppendLine("=== TÀI LIỆU THAM KHẢO ===");
                for (int i = 0; i < chunks.Count; i++)
                {
                    sb.AppendLine($"--- Đoạn {i + 1} ---");
                    sb.AppendLine(chunks[i]);
                    sb.AppendLine();
                }
                sb.AppendLine("=== HẾT TÀI LIỆU ===");
                sb.AppendLine();
            }
            else
            {
                _logger.LogWarning("[RAG] BuildRAGPrompt: chunkContents RỖNG! Đây là nguyên nhân AI từ chối.");
                sb.AppendLine("Chưa có tài liệu được cung cấp.");
                sb.AppendLine();
            }

            if (history != null && history.Count > 0)
            {
                sb.AppendLine("=== LỊCH SỬ HỘI THOẠI ===");
                foreach (var (role, content) in history.TakeLast(4))
                {
                    sb.AppendLine(role == "user" ? $"Sinh viên: {content}" : $"Trợ lý: {content}");
                }
                sb.AppendLine("=== HẾT LỊCH SỬ ===");
                sb.AppendLine();
            }

            sb.AppendLine($"Sinh viên hỏi: {question}");
            sb.AppendLine("Hãy trả lời dựa trên tài liệu ở trên:");

            return sb.ToString();
        }

        private async Task<string> CallGeminiAPIAsync(string prompt)
        {
            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                _logger.LogWarning("GeminiSettings.ApiKey is empty. Falling back to local response.");
                return "Hệ thống chưa được cấu hình khóa API Gemini. Vui lòng liên hệ quản trị viên.";
            }

            var client = _httpClientFactory.CreateClient("GeminiClient");
            client.Timeout = TimeSpan.FromSeconds(60);

            var url = $"{_settings.BaseUrl}/{_settings.Model}:generateContent?key={_settings.ApiKey}";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = _settings.Temperature,
                    maxOutputTokens = _settings.MaxOutputTokens,
                    topP = 0.8,
                    topK = 40
                }
            };

            var json = JsonSerializer.Serialize(requestBody);

            // Retry logic for transient errors (5xx, 429)
            int maxRetries = 3;
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(url, content);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorBody = await response.Content.ReadAsStringAsync();
                        int statusCode = (int)response.StatusCode;
                        _logger.LogError("Gemini API error {Status}: {Body}", response.StatusCode, errorBody);

                        // Retry on 429 (rate limit) with longer delay
                        if (statusCode == 429 && attempt < maxRetries)
                        {
                            int waitSeconds = 10 + (attempt * 5); // 10s, 15s, 20s
                            _logger.LogWarning("Gemini rate limit 429, waiting {Wait}s (attempt {Attempt}/{MaxRetries})...", waitSeconds, attempt + 1, maxRetries);
                            await Task.Delay(waitSeconds * 1000);
                            continue;
                        }

                        // Retry on transient server errors (5xx)
                        if (statusCode >= 500 && attempt < maxRetries)
                        {
                            _logger.LogWarning("Gemini server error {Status}, retrying (attempt {Attempt}/{MaxRetries})...", statusCode, attempt + 1, maxRetries);
                            await Task.Delay(2000 * (attempt + 1));
                            continue;
                        }

                        // Non-retryable error
                        if (statusCode == 429)
                            return "Hệ thống đang bận (API rate limit). Vui lòng đợi 30 giây rồi thử lại.";

                        response.EnsureSuccessStatusCode();
                    }

                    var responseJson = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("[RAG] Raw Gemini API JSON: {Json}", responseJson);

                    using var doc = JsonDocument.Parse(responseJson);
                    var root = doc.RootElement;

                    if (!root.TryGetProperty("candidates", out var candidates) ||
                        candidates.ValueKind != JsonValueKind.Array ||
                        candidates.GetArrayLength() == 0)
                    {
                        _logger.LogWarning("Gemini response không có candidates hợp lệ.");
                        return "Không nhận được phản hồi từ AI.";
                    }

                    var firstCandidate = candidates[0];
                    if (!firstCandidate.TryGetProperty("content", out var contentElement) ||
                        !contentElement.TryGetProperty("parts", out var parts) ||
                        parts.ValueKind != JsonValueKind.Array ||
                        parts.GetArrayLength() == 0)
                    {
                        _logger.LogWarning("Gemini response không có content/parts hợp lệ.");
                        return "Không nhận được phản hồi từ AI.";
                    }

                    // Loop through all parts in the candidate's content to support multi-part text generation (prevent cut-off)
                    var sbText = new StringBuilder();
                    foreach (var part in parts.EnumerateArray())
                    {
                        if (part.TryGetProperty("text", out var txtVal))
                        {
                            sbText.Append(txtVal.GetString());
                        }
                    }

                    var text = sbText.ToString();
                    return string.IsNullOrWhiteSpace(text) ? "Không nhận được phản hồi từ AI." : text;
                }
                catch (TaskCanceledException) when (attempt < maxRetries)
                {
                    _logger.LogWarning("Gemini API timeout (attempt {Attempt}/{MaxRetries}). Retrying...", attempt + 1, maxRetries);
                    await Task.Delay(2000 * (attempt + 1));
                }
                catch (HttpRequestException ex) when (attempt < maxRetries)
                {
                    _logger.LogWarning(ex, "Gemini API connection error (attempt {Attempt}/{MaxRetries}). Retrying...", attempt + 1, maxRetries);
                    await Task.Delay(2000 * (attempt + 1));
                }
            }

            return "Không thể kết nối đến Gemini API sau nhiều lần thử. Vui lòng thử lại.";
        }
    }
}

