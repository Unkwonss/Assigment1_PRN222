using System.Collections.Generic;
using System.Threading.Tasks;

namespace BusinessLayer.Interfaces
{
    public interface IGeminiService
    {
        Task<(string Response, int PromptTokens, int CompletionTokens)> GenerateResponseAsync(
            string userQuestion,
            List<string> contextChunks,
            List<(string role, string content)> conversationHistory,
            string subjectName = ""
        );
    }
}

