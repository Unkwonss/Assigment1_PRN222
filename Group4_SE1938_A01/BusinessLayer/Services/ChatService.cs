using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BusinessLayer.Helpers;
using Domain.Models;
using DataAccessLayer.Repository;
using BusinessLayer.Interfaces;
using BusinessLayer.Services.Embedding;
using Microsoft.Extensions.Logging;
using BusinessLayer.DTOs;

namespace BusinessLayer.Services
{
    public class ChatService : IChatService
    {
        private readonly IGenericRepository<ChatSession> _sessionRepo;
        private readonly IGenericRepository<ChatHistory> _historyRepo;
        private readonly IGenericRepository<ChatCitation> _citationRepo;
        private readonly IGenericRepository<Document> _documentRepo;
        private readonly IGenericRepository<DocumentIndex> _indexRepo;
        private readonly IGenericRepository<DocumentChunk> _chunkRepo;
        private readonly IGenericRepository<Subject> _subjectRepo;
        private readonly IGenericRepository<EmbeddingModel> _modelRepo;
        private readonly SimulatedAIEngine _aiEngine;
        private readonly IGeminiService _geminiService;
        private readonly IGeminiEmbeddingService _embeddingService;
        private readonly EmbeddingProviderFactory _embeddingFactory;
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            IGenericRepository<ChatSession> sessionRepo,
            IGenericRepository<ChatHistory> historyRepo,
            IGenericRepository<ChatCitation> citationRepo,
            IGenericRepository<Document> documentRepo,
            IGenericRepository<DocumentIndex> indexRepo,
            IGenericRepository<DocumentChunk> chunkRepo,
            IGenericRepository<Subject> subjectRepo,
            IGenericRepository<EmbeddingModel> modelRepo,
            SimulatedAIEngine aiEngine,
            IGeminiService geminiService,
            IGeminiEmbeddingService embeddingService,
            EmbeddingProviderFactory embeddingFactory,
            ILogger<ChatService> logger)
        {
            _sessionRepo      = sessionRepo;
            _historyRepo      = historyRepo;
            _citationRepo     = citationRepo;
            _documentRepo     = documentRepo;
            _indexRepo        = indexRepo;
            _chunkRepo        = chunkRepo;
            _subjectRepo      = subjectRepo;
            _modelRepo        = modelRepo;
            _aiEngine         = aiEngine;
            _geminiService    = geminiService;
            _embeddingService = embeddingService;
            _embeddingFactory = embeddingFactory;
            _logger           = logger;
        }

        #region Mappers
        private SubjectDto? MapSubjectToDto(Subject? subject)
        {
            if (subject == null) return null;
            return new SubjectDto
            {
                SubjectId = subject.SubjectId,
                SubjectCode = subject.SubjectCode,
                SubjectName = subject.SubjectName
            };
        }

        private UserDto? MapUserToDto(User? user)
        {
            if (user == null) return null;
            return new UserDto
            {
                UserId = user.UserId,
                Username = user.Username,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role
            };
        }

        private DocumentChunkDto? MapChunkToDto(DocumentChunk? chunk)
        {
            if (chunk == null) return null;
            return new DocumentChunkDto
            {
                ChunkId = chunk.ChunkId,
                IndexId = chunk.IndexId,
                ChunkOrder = chunk.ChunkOrder,
                Content = chunk.Content,
                PageNumber = chunk.PageNumber,
                TokenCount = chunk.TokenCount,
                VectorStoreKey = chunk.VectorStoreKey,
                EmbeddingVector = chunk.EmbeddingVector,
                HasEmbedding = chunk.HasEmbedding,
                Index = chunk.Index != null ? new DocumentIndexDto
                {
                    IndexId = chunk.Index.IndexId,
                    DocumentId = chunk.Index.DocumentId,
                    ModelId = chunk.Index.ModelId,
                    StrategyId = chunk.Index.StrategyId,
                    ChunkSize = chunk.Index.ChunkSize,
                    ChunkOverlap = chunk.Index.ChunkOverlap,
                    CreatedAt = chunk.Index.CreatedAt,
                    Document = chunk.Index.Document != null ? new DocumentDto
                    {
                        DocumentId = chunk.Index.Document.DocumentId,
                        Title = chunk.Index.Document.Title,
                        FileName = chunk.Index.Document.FileName,
                        FilePath = chunk.Index.Document.FilePath,
                        FileType = chunk.Index.Document.FileType
                    } : null
                } : null
            };
        }

        private ChatCitationDto? MapCitationToDto(ChatCitation? citation)
        {
            if (citation == null) return null;
            return new ChatCitationDto
            {
                CitationId = citation.CitationId,
                HistoryId = citation.HistoryId,
                ChunkId = citation.ChunkId,
                PageNumber = citation.PageNumber,
                Snippet = citation.Snippet,
                Chunk = MapChunkToDto(citation.Chunk)
            };
        }

        private ChatHistoryDto? MapHistoryToDto(ChatHistory? history)
        {
            if (history == null) return null;
            return new ChatHistoryDto
            {
                HistoryId = history.HistoryId,
                SessionId = history.SessionId,
                UserMessage = history.UserMessage,
                StandaloneQuery = history.StandaloneQuery,
                BotResponse = history.BotResponse,
                Timestamp = history.Timestamp,
                ChatCitations = history.ChatCitations != null ? history.ChatCitations.Select(c => MapCitationToDto(c)!).ToList() : new List<ChatCitationDto>()
            };
        }

        private ChatSessionDto? MapSessionToDto(ChatSession? session)
        {
            if (session == null) return null;
            return new ChatSessionDto
            {
                SessionId = session.SessionId,
                UserId = session.UserId,
                SubjectId = session.SubjectId,
                Title = session.Title,
                ConversationSummary = session.ConversationSummary,
                CreatedAt = session.CreatedAt,
                LastUpdatedAt = session.LastUpdatedAt,
                Subject = MapSubjectToDto(session.Subject),
                User = MapUserToDto(session.User),
                ChatHistories = session.ChatHistories != null ? session.ChatHistories.Select(h => MapHistoryToDto(h)!).ToList() : new List<ChatHistoryDto>()
            };
        }
        #endregion

        public async Task<ChatSessionDto> CreateSessionAsync(int userId, int subjectId, string title = "Cuộc trò chuyện mới")
        {
            var session = new ChatSession
            {
                SessionId = Guid.NewGuid(),
                UserId = userId,
                SubjectId = subjectId,
                Title = title,
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow
            };

            await _sessionRepo.AddAsync(session);
            await _sessionRepo.SaveAsync();
            return MapSessionToDto(session)!;
        }

        public async Task<IEnumerable<ChatSessionDto>> GetSessionsAsync(int userId, int subjectId)
        {
            var sessions = await _sessionRepo.GetAllAsync(
                filter: s => s.UserId == userId && s.SubjectId == subjectId,
                orderBy: q => q.OrderByDescending(s => s.LastUpdatedAt)
            );
            return sessions.Select(s => MapSessionToDto(s)!).ToList();
        }

        public async Task<ChatSessionDto?> GetSessionByIdAsync(Guid sessionId)
        {
            var session = await _sessionRepo.GetFirstOrDefaultAsync(
                filter: s => s.SessionId == sessionId,
                includeProperties: "Subject,User"
            );
            return MapSessionToDto(session);
        }

        public async Task RenameSessionAsync(Guid sessionId, string newTitle)
        {
            var session = await _sessionRepo.GetByIdAsync(sessionId);
            if (session != null)
            {
                session.Title = newTitle;
                session.LastUpdatedAt = DateTime.UtcNow;
                _sessionRepo.Update(session);
                await _sessionRepo.SaveAsync();
            }
        }

        public async Task DeleteSessionAsync(Guid sessionId)
        {
            await _sessionRepo.DeleteByIdAsync(sessionId);
            await _sessionRepo.SaveAsync();
        }

        public async Task<IEnumerable<ChatHistoryDto>> GetChatHistoryAsync(Guid sessionId)
        {
            var history = await _historyRepo.GetAllAsync(
                filter: h => h.SessionId == sessionId,
                orderBy: q => q.OrderBy(h => h.Timestamp),
                includeProperties: "ChatCitations,ChatCitations.Chunk,ChatCitations.Chunk.Index,ChatCitations.Chunk.Index.Document"
            );
            return history.Select(h => MapHistoryToDto(h)!).ToList();
        }

        public async Task<ChatHistoryDto> SendMessageAsync(Guid sessionId, string userMessage, int embeddingModelId, int strategyId, int chunkSize, int chunkOverlap)
        {
            var result = await SendMessageWithScoresAsync(sessionId, userMessage, embeddingModelId, strategyId, chunkSize, chunkOverlap);
            return result.History;
        }

        public async Task<(ChatHistoryDto History, List<(ChatCitationDto Citation, float Score)> Citations)> SendMessageWithScoresAsync(
            Guid sessionId,
            string userMessage,
            int embeddingModelId,
            int strategyId,
            int chunkSize,
            int chunkOverlap)
        {
            var session = await _sessionRepo.GetFirstOrDefaultAsync(s => s.SessionId == sessionId, "Subject");
            if (session == null) throw new ArgumentException("Phiên trò chuyện không tồn tại.");

            string subjectCode = session.Subject?.SubjectCode ?? "PRN222";
            int subjectId = session.SubjectId;
            string botResponse = "";
            var relevantChunks = await GetRelevantChunksAsync(
                userMessage, subjectId, embeddingModelId, strategyId, chunkSize, chunkOverlap, topK: 3);

            _logger.LogInformation(
                "[RAG-PIPELINE] Chunks retrieved: {Count}, Question: {Question}, ModelId={ModelId}, StrategyId={StrategyId}, ChunkSize={ChunkSize}, ChunkOverlap={ChunkOverlap}",
                relevantChunks?.Count ?? -1,
                userMessage.Length > 80 ? userMessage[..80] : userMessage,
                embeddingModelId, strategyId, chunkSize, chunkOverlap);

            if (relevantChunks != null && relevantChunks.Any())
            {
                _logger.LogInformation(
                    "[RAG-PIPELINE] Chunk 1 preview: {Preview}",
                    relevantChunks[0].Chunk.Content?.Length > 100
                        ? relevantChunks[0].Chunk.Content[..100]
                        : relevantChunks[0].Chunk.Content);
            }

            if (relevantChunks == null)
            {
                // Dimension mismatch: tất cả chunk bị loại vì index bằng model khác
                botResponse = "⚠️ Tài liệu chưa được index với model này. " +
                              "Vui lòng chọn lại tài liệu và nhấn \"Re-Index\" với model đang chọn.";
            }
            else if (!relevantChunks.Any())
            {
                botResponse = "Không tìm thấy nội dung liên quan trong tài liệu đã index. " +
                              "Vui lòng đặt câu hỏi cụ thể hơn hoặc kiểm tra lại tài liệu nguồn.";
            }
            else if (relevantChunks != null)
            {
                var contextTexts = relevantChunks.Select(c => c.Chunk.Content).ToList();
                var maxScore = relevantChunks.Max(c => c.Score);

                _logger.LogInformation(
                    "[RAG-PIPELINE] contextTexts count: {Count}, maxScore: {Score}",
                    contextTexts.Count, maxScore);

                if (maxScore < 0.45f)
                {
                    contextTexts.Insert(0, "Lưu ý: nội dung tìm được có thể không hoàn toàn chính xác với câu hỏi.");
                }

                var recentHistory = await _historyRepo.GetAllAsync(
                    h => h.SessionId == sessionId,
                    orderBy: q => q.OrderBy(h => h.Timestamp)
                );
                var historyTuples = new List<(string role, string content)>();
                foreach (var h in recentHistory.TakeLast(5))
                {
                    historyTuples.Add(("user", h.UserMessage));
                    if (!string.IsNullOrWhiteSpace(h.BotResponse))
                    {
                        historyTuples.Add(("assistant", h.BotResponse));
                    }
                }

                _logger.LogInformation(
                    "[RAG-PIPELINE] Calling GeminiService with {ChunkCount} chunks, {HistoryCount} history items, subject={Subject}",
                    contextTexts.Count, historyTuples.Count, subjectCode);

                botResponse = await _geminiService.GenerateResponseAsync(
                    userMessage,
                    contextTexts,
                    historyTuples,
                    subjectCode
                );
            }

            // Save Chat History
            var history = new ChatHistory
            {
                SessionId = sessionId,
                UserMessage = userMessage,
                StandaloneQuery = userMessage,
                BotResponse = botResponse,
                Timestamp = DateTime.UtcNow
            };

            await _historyRepo.AddAsync(history);
            await _historyRepo.SaveAsync(); // Saves to get HistoryId

            // Save Citations if any match
            var citationsWithScore = new List<(ChatCitationDto Citation, float Score)>();
            if (relevantChunks != null && relevantChunks.Any())
            {
                foreach (var tc in relevantChunks)
                {
                    var citation = new ChatCitation
                    {
                        HistoryId = history.HistoryId,
                        ChunkId = tc.Chunk.ChunkId,
                        PageNumber = tc.Chunk.PageNumber,
                        Snippet = tc.Chunk.Content.Length > 200 ? tc.Chunk.Content.Substring(0, 197) + "..." : tc.Chunk.Content
                    };
                    await _citationRepo.AddAsync(citation);
                    
                    var citationDto = new ChatCitationDto
                    {
                        CitationId = citation.CitationId, // note: will be populated correctly or after save
                        HistoryId = citation.HistoryId,
                        ChunkId = citation.ChunkId,
                        PageNumber = citation.PageNumber,
                        Snippet = citation.Snippet,
                        Chunk = MapChunkToDto(tc.Chunk)
                    };
                    citationsWithScore.Add((citationDto, tc.Score));
                }
                await _citationRepo.SaveAsync();

                // update ids after save
                for (int i = 0; i < citationsWithScore.Count; i++)
                {
                    // If DB generates identity CitationId, we can reload them if needed, 
                    // but for view display, populated info is mostly enough.
                }
            }

            // Update session last updated time
            session.LastUpdatedAt = DateTime.UtcNow;
            _sessionRepo.Update(session);
            await _sessionRepo.SaveAsync();

            return (MapHistoryToDto(history)!, citationsWithScore);
        }

        private async Task<List<(DocumentChunk Chunk, float Score)>?> GetRelevantChunksAsync(
            string question,
            int subjectId,
            int embeddingModelId,
            int strategyId,
            int chunkSize,
            int chunkOverlap,
            int topK = 3)
        {
            var embeddingModel = await _modelRepo.GetByIdAsync(embeddingModelId);
            IEmbeddingProvider embeddingProvider;
            if (embeddingModel != null)
            {
                embeddingProvider = _embeddingFactory.GetProvider(embeddingModel.ModelName ?? string.Empty);
                _logger.LogInformation(
                    "[RAG-EMBED-QUERY] Provider={Provider}, Model={Model}",
                    embeddingProvider.ProviderName, embeddingProvider.ModelName);
            }
            else
            {
                _logger.LogWarning("[RAG-EMBED-QUERY] EmbeddingModel id={Id} not found, falling back to Gemini.", embeddingModelId);
                embeddingProvider = _embeddingFactory.GetProvider("gemini-embedding-001");
            }

            var questionVector = await embeddingProvider.GetEmbeddingAsync(question);
            bool useVectorSearch = questionVector.Length > 0;

            var matchingChunks = (await _chunkRepo.GetAllAsync(
                filter: c => c.Index.Document.Chapter.SubjectId == subjectId &&
                             c.Index.Document.Status == "Indexed" &&
                             c.Index.ModelId == embeddingModelId &&
                             c.Index.StrategyId == strategyId &&
                             c.Index.ChunkSize == chunkSize &&
                             c.Index.ChunkOverlap == chunkOverlap &&
                             c.Content != null &&
                             c.Content.Length > 0,
                includeProperties: "Index,Index.Document,Index.Document.Chapter"))
                .ToList();

            _logger.LogInformation(
                "[RAG-RETRIEVAL] Exact config match: {Count} chunks (ModelId={ModelId}, StrategyId={StrategyId}, Size={Size}, Overlap={Overlap})",
                matchingChunks.Count, embeddingModelId, strategyId, chunkSize, chunkOverlap);

            if (!matchingChunks.Any())
            {
                matchingChunks = (await _chunkRepo.GetAllAsync(
                    filter: c => c.Index.Document.Chapter.SubjectId == subjectId &&
                                 c.Index.Document.Status == "Indexed" &&
                                 c.Index.ModelId == embeddingModelId &&
                                 c.Index.StrategyId == strategyId &&
                                 c.Content != null &&
                                 c.Content.Length > 0,
                    includeProperties: "Index,Index.Document,Index.Document.Chapter"))
                    .ToList();

                _logger.LogInformation(
                    "[RAG-RETRIEVAL] Model+Strategy match: {Count} chunks", matchingChunks.Count);
            }

            if (!matchingChunks.Any())
            {
                matchingChunks = (await _chunkRepo.GetAllAsync(
                    filter: c => c.Index.Document.Chapter.SubjectId == subjectId &&
                                 c.Index.Document.Status == "Indexed" &&
                                 c.Content != null &&
                                 c.Content.Length > 0,
                    includeProperties: "Index,Index.Document,Index.Document.Chapter"))
                    .ToList();

                _logger.LogWarning(
                    "[RAG-RETRIEVAL] No matching config found, falling back to ALL {Count} chunks for subject", matchingChunks.Count);
            }

            if (!matchingChunks.Any())
            {
                _logger.LogWarning("Không có chunk nào cho subjectId={Id}", subjectId);
                return new List<(DocumentChunk Chunk, float Score)>();
            }

            List<(DocumentChunk Chunk, float Score)> result;
            if (useVectorSearch)
            {
                _logger.LogInformation("Dùng vector search cho {Count} chunks", matchingChunks.Count);

                var dimensionMismatched = 0;
                var scored = new List<(DocumentChunk Chunk, float Score)>();

                foreach (var chunk in matchingChunks)
                {
                    if (!chunk.HasEmbedding || string.IsNullOrEmpty(chunk.EmbeddingVector))
                        continue;

                    var chunkVector = DeserializeVector(chunk.EmbeddingVector);

                    if (chunkVector.Length != questionVector.Length)
                    {
                        dimensionMismatched++;
                        _logger.LogWarning(
                            "[DIM-MISMATCH] Chunk#{ChunkId} dim={ChunkDim} != query dim={QueryDim}. Bỏ qua.",
                            chunk.ChunkId, chunkVector.Length, questionVector.Length);
                        continue;
                    }

                    var score = VectorHelper.CosineSimilarity(questionVector, chunkVector);
                    scored.Add((chunk, score));
                }

                if (dimensionMismatched > 0 && scored.Count == 0)
                {
                    return null; // mismatch, caller trả thông báo re-index
                }

                result = scored
                    .OrderByDescending(x => x.Score)
                    .Take(topK)
                    .Where(x => x.Score > 0.4f)
                    .ToList();

                if (!result.Any())
                {
                    result = KeywordSearch(matchingChunks, question, topK);
                }
            }
            else
            {
                result = KeywordSearch(matchingChunks, question, topK);
            }

            return result;
        }

        private List<(DocumentChunk Chunk, float Score)> KeywordSearch(
            List<DocumentChunk> chunks,
            string question,
            int topK)
        {
            var keywords = question.ToLower()
                .Split(new[] { ' ', '?', ',', '.', ';', ':', '!' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2)
                .ToList();

            return chunks
                .Select(c => (Chunk: c, Score: (float)keywords.Count(k => c.Content.ToLower().Contains(k))))
                .Where(x => x.Score > 0f)
                .OrderByDescending(x => x.Score)
                .Take(topK)
                .ToList();
        }

        private float[] DeserializeVector(string? json)
        {
            if (string.IsNullOrEmpty(json)) return Array.Empty<float>();
            return JsonSerializer.Deserialize<float[]>(json) ?? Array.Empty<float>();
        }
    }
}
