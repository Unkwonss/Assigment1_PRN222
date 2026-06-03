using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BusinessLayer.Interfaces;

namespace BusinessLayer.Services.Embedding
{
    /// <summary>
    /// Adapter bọc logic của GeminiEmbeddingService thành IEmbeddingProvider.
    /// Model: gemini-embedding-001 (768-dim).
    /// API:   https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-001:embedContent
    /// </summary>
    public class GeminiEmbeddingAdapter : IEmbeddingProvider
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        private const string ModelId  = "models/gemini-embedding-001";
        private const string BaseUrl  = "https://generativelanguage.googleapis.com/v1beta";

        public string ModelName   => "gemini-embedding-001";
        public string ProviderName => "Gemini";
        public int    Dimensions   => 768;

        public GeminiEmbeddingAdapter(HttpClient httpClient, string apiKey)
        {
            _httpClient = httpClient;
            _apiKey     = apiKey;
        }

        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
                return Array.Empty<float>();

            var url  = $"{BaseUrl}/{ModelId}:embedContent?key={_apiKey}";
            var body = new
            {
                model   = ModelId,
                content = new { parts = new[] { new { text } } }
            };

            using var content  = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);
            if (!response.IsSuccessStatusCode)
                return Array.Empty<float>();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var values = doc.RootElement
                .GetProperty("embedding")
                .GetProperty("values");

            return values.EnumerateArray().Select(v => v.GetSingle()).ToArray();
        }

        public async Task<List<float[]>> GetEmbeddingsAsync(List<string> texts)
        {
            var results = new List<float[]>();
            foreach (var text in texts)
            {
                results.Add(await GetEmbeddingAsync(text));
                await Task.Delay(200); // rate-limit guard
            }
            return results;
        }
    }
}
