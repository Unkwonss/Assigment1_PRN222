using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BusinessLayer.Interfaces;

namespace BusinessLayer.Services.Embedding
{
    public class OpenAIEmbeddingProvider : IEmbeddingProvider
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _modelName;
        private readonly int _dimensions;

        public string ModelName => _modelName;
        public string ProviderName => "OpenAI";
        public int Dimensions => _dimensions;

        public OpenAIEmbeddingProvider(HttpClient httpClient, string apiKey, string modelName = "text-embedding-3-small", int dimensions = 1536)
        {
            _httpClient = httpClient;
            _apiKey = apiKey;
            _modelName = modelName;
            _dimensions = dimensions;
            
            if (!_httpClient.DefaultRequestHeaders.Contains("Authorization"))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            }
        }

        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            var embeddings = await GetEmbeddingsAsync(new List<string> { text });
            return embeddings.Count > 0 ? embeddings[0] : Array.Empty<float>();
        }

        public async Task<List<float[]>> GetEmbeddingsAsync(List<string> texts)
        {
            var requestBody = new
            {
                model = _modelName,
                input = texts
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/embeddings")
            {
                Content = jsonContent
            };
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            var response = await _httpClient.SendAsync(requestMessage);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseBody);
            
            var results = new List<float[]>();
            var dataArray = doc.RootElement.GetProperty("data");
            
            foreach (var element in dataArray.EnumerateArray())
            {
                var embeddingArray = element.GetProperty("embedding");
                var vector = new float[embeddingArray.GetArrayLength()];
                int i = 0;
                foreach (var val in embeddingArray.EnumerateArray())
                {
                    vector[i++] = val.GetSingle();
                }
                results.Add(vector);
            }

            return results;
        }
    }
}
