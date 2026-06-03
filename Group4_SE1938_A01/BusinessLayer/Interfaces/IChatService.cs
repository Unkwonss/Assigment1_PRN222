using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BusinessLayer.DTOs;

namespace BusinessLayer.Interfaces
{
    public interface IChatService
    {
        // Sessions
        Task<ChatSessionDto> CreateSessionAsync(int userId, int subjectId, string title = "Cuộc trò chuyện mới");
        Task<IEnumerable<ChatSessionDto>> GetSessionsAsync(int userId, int subjectId);
        Task<ChatSessionDto?> GetSessionByIdAsync(Guid sessionId);
        Task RenameSessionAsync(Guid sessionId, string newTitle);
        Task DeleteSessionAsync(Guid sessionId);

        // History and Citations
        Task<IEnumerable<ChatHistoryDto>> GetChatHistoryAsync(Guid sessionId);
        Task<ChatHistoryDto> SendMessageAsync(Guid sessionId, string userMessage, int embeddingModelId, int strategyId, int chunkSize, int chunkOverlap);
        Task<(ChatHistoryDto History, List<(ChatCitationDto Citation, float Score)> Citations)> SendMessageWithScoresAsync(Guid sessionId, string userMessage, int embeddingModelId, int strategyId, int chunkSize, int chunkOverlap);
    }
}
