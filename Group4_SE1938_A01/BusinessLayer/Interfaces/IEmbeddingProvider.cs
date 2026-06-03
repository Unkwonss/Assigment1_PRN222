using System.Collections.Generic;
using System.Threading.Tasks;

namespace BusinessLayer.Interfaces
{
    /// <summary>
    /// Interface chuẩn hoá cho tất cả Embedding Provider.
    /// Mỗi provider (OpenAI, HuggingFace, Ollama) sẽ implement interface này.
    /// </summary>
    public interface IEmbeddingProvider
    {
        /// <summary>Tên hiển thị của model (ví dụ: "text-embedding-3-small")</summary>
        string ModelName { get; }

        /// <summary>Tên provider (ví dụ: "OpenAI", "HuggingFace", "Ollama")</summary>
        string ProviderName { get; }

        /// <summary>Số chiều (dimensions) của vector embedding</summary>
        int Dimensions { get; }

        /// <summary>
        /// Tạo embedding vector cho một đoạn text.
        /// </summary>
        Task<float[]> GetEmbeddingAsync(string text);

        /// <summary>
        /// Tạo embedding vectors cho nhiều đoạn text cùng lúc (batch).
        /// Triển khai mặc định gọi GetEmbeddingAsync từng cái.
        /// </summary>
        Task<List<float[]>> GetEmbeddingsAsync(List<string> texts);
    }
}
