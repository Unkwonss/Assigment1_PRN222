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
    public class HuggingFaceEmbeddingProvider : IEmbeddingProvider
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _modelName; // e.g., "intfloat/multilingual-e5-base" or "BAAI/bge-m3"
        private readonly int _dimensions;
        private readonly string _endpointUrl;
        private readonly string _textPrefix; // e.g., "query: " required by e5 models

        public string ModelName    => _modelName;
        public string ProviderName => "HuggingFace";
        public int    Dimensions   => _dimensions;

        public HuggingFaceEmbeddingProvider(
            HttpClient httpClient,
            string apiKey,
            string modelName  = "intfloat/multilingual-e5-base",
            int    dimensions = 768,
            string textPrefix = "")
        {
            _httpClient  = httpClient;
            _apiKey      = apiKey;
            _modelName   = modelName;
            _dimensions  = dimensions;
            _textPrefix  = textPrefix;
            _endpointUrl = $"https://api-inference.huggingface.co/pipeline/feature-extraction/{_modelName}";
        }

        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            var embeddings = await GetEmbeddingsAsync(new List<string> { text });
            return embeddings.Count > 0 ? embeddings[0] : Array.Empty<float>();
        }

        public async Task<List<float[]>> GetEmbeddingsAsync(List<string> texts)
        {
            // Apply prefix if required (e.g. e5 models need "query: " or "passage: ")
            var preparedTexts = string.IsNullOrEmpty(_textPrefix)
                ? texts
                : texts.Select(t => _textPrefix + t).ToList();

            var requestBody = new
            {
                inputs = preparedTexts
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, _endpointUrl)
            {
                Content = jsonContent
            };
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            var response = await _httpClient.SendAsync(requestMessage);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseBody);
            
            var results = new List<float[]>();
            
            // HuggingFace returns a list of lists of floats
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var vector = new float[element.GetArrayLength()];
                int i = 0;
                foreach (var val in element.EnumerateArray())
                {
                    vector[i++] = val.GetSingle();
                }
                results.Add(vector);
            }

            return results;
        }
    }
}
