using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BusinessLayer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BusinessLayer.Services
{
    public class GeminiEmbeddingService : IGeminiEmbeddingService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _apiKey;
        private readonly ILogger<GeminiEmbeddingService> _logger;
        // text-embedding-004 retired (Jan 2026). Use gemini-embedding-001.
        private static readonly string[] Models = new[] { "models/gemini-embedding-001" };
        private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta";

        public GeminiEmbeddingService(
            IHttpClientFactory httpClientFactory,
            IConfiguration config,
            ILogger<GeminiEmbeddingService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _apiKey = config["GeminiSettings:ApiKey"] ?? string.Empty;
            _logger = logger;
        }

        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_apiKey))
                {
                    _logger.LogWarning("Gemini API key is empty. Cannot generate embeddings.");
                    return Array.Empty<float>();
                }

                var client = _httpClientFactory.CreateClient("GeminiClient");
                foreach (var model in Models)
                {
                    var url = $"{BaseUrl}/{model}:embedContent?key={_apiKey}";
                    var body = new
                    {
                        model,
                        content = new
                        {
                            parts = new[] { new { text } }
                        }
                    };

                    var json = JsonSerializer.Serialize(body);
                    using var requestBody = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(url, requestBody);
                    var responseJson = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Embedding API error for model {Model}: {Body}", model, responseJson);
                        continue;
                    }

                    using var doc = JsonDocument.Parse(responseJson);
                    var values = doc.RootElement
                        .GetProperty("embedding")
                        .GetProperty("values");

                    return values.EnumerateArray()
                        .Select(v => v.GetSingle())
                        .ToArray();
                }

                return Array.Empty<float>();
            }
            catch (Exception ex)
            {
                var preview = text.Length > 50 ? text[..50] : text;
                _logger.LogError(ex, "Lỗi khi tạo embedding cho text: {Text}", preview);
                return Array.Empty<float>();
            }
        }

        public async Task<List<float[]>> GetEmbeddingsBatchAsync(List<string> texts)
        {
            var results = new List<float[]>();
            foreach (var text in texts)
            {
                var embedding = await GetEmbeddingAsync(text);
                results.Add(embedding);
                await Task.Delay(200);
            }
            return results;
        }
    }
}

