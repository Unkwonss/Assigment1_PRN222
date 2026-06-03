using System.Collections.Generic;
using System.Threading.Tasks;

namespace BusinessLayer.Interfaces
{
    public interface IGeminiService
    {
        Task<string> GenerateResponseAsync(
            string userQuestion,
            List<string> contextChunks,
            List<(string role, string content)> conversationHistory,
            string subjectName = ""
        );
    }
}

