using System.Collections.Generic;
using System.Threading.Tasks;

namespace BusinessLayer.Interfaces
{
    public interface IGeminiEmbeddingService
    {
        Task<float[]> GetEmbeddingAsync(string text);
        Task<List<float[]>> GetEmbeddingsBatchAsync(List<string> texts);
    }
}

