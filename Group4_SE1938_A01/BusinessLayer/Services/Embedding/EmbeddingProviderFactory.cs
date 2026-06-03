using System;
using System.Net.Http;
using BusinessLayer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BusinessLayer.Services.Embedding
{
    /// <summary>
    /// Factory chọn đúng IEmbeddingProvider dựa trên modelName lưu trong DB (EmbeddingModel.ModelName).
    /// Mapping:
    ///   "gemini-embedding-001"       → GeminiEmbeddingAdapter  (Gemini API — đang hoạt động)
    ///   "multilingual-e5-base"       → HuggingFaceEmbeddingProvider (HF Inference API)
    ///   "bge-m3"                     → HuggingFaceEmbeddingProvider (HF Inference API)
    ///   "text-embedding-3-small"     → OpenAIEmbeddingProvider  (OpenAI API)
    ///   "PhoBERT-base"               → HuggingFaceEmbeddingProvider (vinai/phobert-base — qua HF)
    ///   default / unknown            → GeminiEmbeddingAdapter  (fallback)
    /// </summary>
    public class EmbeddingProviderFactory
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmbeddingProviderFactory> _logger;

        public EmbeddingProviderFactory(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<EmbeddingProviderFactory> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Trả về provider phù hợp theo modelName. 
        /// modelName phải khớp với cột EmbeddingModel.ModelName trong DB.
        /// </summary>
        public IEmbeddingProvider GetProvider(string modelName)
        {
            var normalized = (modelName ?? string.Empty).Trim().ToLowerInvariant();
            _logger.LogWarning("[EmbeddingFactory] Bypassing '{ModelName}' and forcing Gemini due to network/API constraints.", modelName);

            return CreateGeminiAdapter();
        }

        // ─────────────────────────────────────────────────────────────
        // Private factory helpers
        // ─────────────────────────────────────────────────────────────

        private IEmbeddingProvider CreateHuggingFaceProvider(string hfModelId, int dimensions, string prefix = "")
        {
            var token = _configuration["HuggingFaceToken"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning(
                    "[EmbeddingFactory] HuggingFaceToken is empty. Provider for '{Model}' will fail at runtime. " +
                    "Add 'HuggingFaceToken' to appsettings.json.", hfModelId);
            }

            var httpClient = _httpClientFactory.CreateClient("HuggingFaceClient");
            return new HuggingFaceEmbeddingProvider(httpClient, token, hfModelId, dimensions, prefix);
        }

        private IEmbeddingProvider CreateOpenAIProvider(string modelId, int dimensions)
        {
            var apiKey = _configuration["OpenAIKey"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning(
                    "[EmbeddingFactory] OpenAIKey is empty. Provider for '{Model}' will fail at runtime. " +
                    "Add 'OpenAIKey' to appsettings.json.", modelId);
            }

            var httpClient = _httpClientFactory.CreateClient("OpenAIClient");
            return new OpenAIEmbeddingProvider(httpClient, apiKey, modelId, dimensions);
        }

        private IEmbeddingProvider CreateGeminiAdapter()
        {
            var apiKey = _configuration["GeminiSettings:ApiKey"] ?? string.Empty;
            var httpClient = _httpClientFactory.CreateClient("GeminiClient");
            return new GeminiEmbeddingAdapter(httpClient, apiKey);
        }

        private IEmbeddingProvider CreateGeminiAdapterWithWarning(string? unknownModel)
        {
            _logger.LogWarning(
                "[EmbeddingFactory] Unknown model '{Model}'. Falling back to Gemini embedding.", unknownModel);
            return CreateGeminiAdapter();
        }
    }
}
