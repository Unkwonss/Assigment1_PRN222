using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BusinessLayer.Interfaces;

namespace BusinessLayer.Services.Embedding
{
    public class OllamaEmbeddingProvider : IEmbeddingProvider
    {
        private readonly HttpClient _httpClient;
        private readonly string _modelName;
        private readonly int _dimensions;
        private readonly string _endpointUrl;

        public string ModelName => _modelName;
        public string ProviderName => "Ollama";
        public int Dimensions => _dimensions;

        public OllamaEmbeddingProvider(HttpClient httpClient, string endpointUrl = "http://localhost:11434/api/embeddings", string modelName = "nomic-embed-text", int dimensions = 768)
        {
            _httpClient = httpClient;
            _endpointUrl = endpointUrl;
            _modelName = modelName;
            _dimensions = dimensions;
        }

        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            var requestBody = new
            {
                model = _modelName,
                prompt = text
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_endpointUrl, jsonContent);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseBody);
            
            var embeddingArray = doc.RootElement.GetProperty("embedding");
            var vector = new float[embeddingArray.GetArrayLength()];
            int i = 0;
            foreach (var val in embeddingArray.EnumerateArray())
            {
                vector[i++] = val.GetSingle();
            }

            return vector;
        }

        public async Task<List<float[]>> GetEmbeddingsAsync(List<string> texts)
        {
            // Ollama current API (/api/embeddings) only supports one prompt at a time.
            // We need to call it iteratively or concurrently.
            var results = new List<float[]>();
            
            // To be safe with local resources, we process sequentially, but can easily be parallelized
            foreach(var text in texts)
            {
                results.Add(await GetEmbeddingAsync(text));
            }
            
            return results;
        }
    }
}
