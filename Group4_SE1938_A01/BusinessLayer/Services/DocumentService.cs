using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Domain.Models;
using DataAccessLayer.Repository;
using BusinessLayer.Interfaces;
using BusinessLayer.Services.Chunking;
using BusinessLayer.Services.Embedding;
using BusinessLayer.DTOs;

namespace BusinessLayer.Services
{
    public class DocumentService : IDocumentService
    {
        private readonly IGenericRepository<Subject> _subjectRepo;
        private readonly IGenericRepository<SubjectTeacher> _subjectTeacherRepo;
        private readonly IGenericRepository<Chapter> _chapterRepo;
        private readonly IGenericRepository<Document> _documentRepo;
        private readonly IGenericRepository<DocumentIndex> _indexRepo;
        private readonly IGenericRepository<DocumentChunk> _chunkRepo;
        private readonly IGenericRepository<ChatCitation> _citationRepo;
        private readonly IGenericRepository<ChunkingStrategy> _strategyRepo;
        private readonly IGenericRepository<EmbeddingModel> _modelRepo;
        private readonly SimulatedAIEngine _aiEngine;
        private readonly IGeminiEmbeddingService _embeddingService; // kept for non-factory fallback
        private readonly EmbeddingProviderFactory _embeddingFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DocumentService> _logger;

        public DocumentService(
            IGenericRepository<Subject> subjectRepo,
            IGenericRepository<SubjectTeacher> subjectTeacherRepo,
            IGenericRepository<Chapter> chapterRepo,
            IGenericRepository<Document> documentRepo,
            IGenericRepository<DocumentIndex> indexRepo,
            IGenericRepository<DocumentChunk> chunkRepo,
            IGenericRepository<ChatCitation> citationRepo,
            IGenericRepository<ChunkingStrategy> strategyRepo,
            IGenericRepository<EmbeddingModel> modelRepo,
            SimulatedAIEngine aiEngine,
            IGeminiEmbeddingService embeddingService,
            EmbeddingProviderFactory embeddingFactory,
            IConfiguration configuration,
            ILogger<DocumentService> logger)
        {
            _subjectRepo      = subjectRepo;
            _subjectTeacherRepo = subjectTeacherRepo;
            _chapterRepo      = chapterRepo;
            _documentRepo     = documentRepo;
            _indexRepo        = indexRepo;
            _chunkRepo        = chunkRepo;
            _citationRepo     = citationRepo;
            _strategyRepo     = strategyRepo;
            _modelRepo        = modelRepo;
            _aiEngine         = aiEngine;
            _embeddingService = embeddingService;
            _embeddingFactory = embeddingFactory;
            _configuration    = configuration;
            _logger           = logger;
        }

        #region Mappers
        private SubjectDto? MapSubjectToDto(Subject? subject)
        {
            if (subject == null) return null;
            
            var headTeacher = subject.SubjectTeachers?.FirstOrDefault(st => st.IsSubjectHead);
            
            return new SubjectDto
            {
                SubjectId = subject.SubjectId,
                SubjectCode = subject.SubjectCode,
                SubjectName = subject.SubjectName,
                ManagedByUserId = headTeacher?.UserId,
                ManagedByUserName = headTeacher?.User?.FullName,
                AssignedTeacherIds = subject.SubjectTeachers?.Select(st => st.UserId).ToList() ?? new List<int>(),
                AssignedTeachers = subject.SubjectTeachers?.Select(st => new UserDto
                {
                    UserId = st.UserId,
                    FullName = st.User?.FullName ?? "",
                    Email = st.User?.Email ?? "",
                    Role = st.User?.Role ?? ""
                }).ToList() ?? new List<UserDto>(),
                Chapters = subject.Chapters != null ? subject.Chapters.Select(c => new ChapterDto
                {
                    ChapterId = c.ChapterId,
                    SubjectId = c.SubjectId,
                    ChapterNumber = c.ChapterNumber,
                    ChapterName = c.ChapterName
                }).ToList() : new List<ChapterDto>()
            };
        }

        private Subject? MapSubjectToEntity(SubjectDto? dto)
        {
            if (dto == null) return null;
            return new Subject
            {
                SubjectId = dto.SubjectId,
                SubjectCode = dto.SubjectCode,
                SubjectName = dto.SubjectName
            };
        }

        private ChapterDto? MapChapterToDto(Chapter? chapter)
        {
            if (chapter == null) return null;
            return new ChapterDto
            {
                ChapterId = chapter.ChapterId,
                SubjectId = chapter.SubjectId,
                ChapterNumber = chapter.ChapterNumber,
                ChapterName = chapter.ChapterName,
                Subject = chapter.Subject != null ? new SubjectDto
                {
                    SubjectId = chapter.Subject.SubjectId,
                    SubjectCode = chapter.Subject.SubjectCode,
                    SubjectName = chapter.Subject.SubjectName
                } : null
            };
        }

        private Chapter? MapChapterToEntity(ChapterDto? dto)
        {
            if (dto == null) return null;
            return new Chapter
            {
                ChapterId = dto.ChapterId,
                SubjectId = dto.SubjectId,
                ChapterNumber = dto.ChapterNumber,
                ChapterName = dto.ChapterName
            };
        }

        private DocumentDto? MapDocumentToDto(Document? doc)
        {
            if (doc == null) return null;
            return new DocumentDto
            {
                DocumentId = doc.DocumentId,
                ChapterId = doc.ChapterId,
                Title = doc.Title,
                FileName = doc.FileName,
                FilePath = doc.FilePath,
                FileType = doc.FileType,
                FileSize = doc.FileSize,
                TotalPages = doc.TotalPages,
                Status = doc.Status,
                UploadedBy = doc.UploadedBy,
                CreatedAt = doc.CreatedAt,
                Chapter = doc.Chapter != null ? new ChapterDto
                {
                    ChapterId = doc.Chapter.ChapterId,
                    SubjectId = doc.Chapter.SubjectId,
                    ChapterNumber = doc.Chapter.ChapterNumber,
                    ChapterName = doc.Chapter.ChapterName,
                    Subject = doc.Chapter.Subject != null ? new SubjectDto
                    {
                        SubjectId = doc.Chapter.Subject.SubjectId,
                        SubjectCode = doc.Chapter.Subject.SubjectCode,
                        SubjectName = doc.Chapter.Subject.SubjectName
                    } : null
                } : null,
                UploadedByNavigation = doc.UploadedByNavigation != null ? new UserDto
                {
                    UserId = doc.UploadedByNavigation.UserId,
                    Username = doc.UploadedByNavigation.Username,
                    FullName = doc.UploadedByNavigation.FullName,
                    Email = doc.UploadedByNavigation.Email,
                    Role = doc.UploadedByNavigation.Role
                } : null
            };
        }

        private Document? MapDocumentToEntity(DocumentDto? dto)
        {
            if (dto == null) return null;
            return new Document
            {
                DocumentId = dto.DocumentId,
                ChapterId = dto.ChapterId,
                Title = dto.Title,
                FileName = dto.FileName,
                FilePath = dto.FilePath,
                FileType = dto.FileType,
                FileSize = dto.FileSize,
                TotalPages = dto.TotalPages,
                Status = dto.Status,
                UploadedBy = dto.UploadedBy,
                CreatedAt = dto.CreatedAt
            };
        }

        private ChunkingStrategyDto? MapStrategyToDto(ChunkingStrategy? strategy)
        {
            if (strategy == null) return null;
            return new ChunkingStrategyDto
            {
                StrategyId = strategy.StrategyId,
                StrategyName = strategy.StrategyName
            };
        }

        private ChunkingStrategy? MapStrategyToEntity(ChunkingStrategyDto? dto)
        {
            if (dto == null) return null;
            return new ChunkingStrategy
            {
                StrategyId = dto.StrategyId,
                StrategyName = dto.StrategyName
            };
        }

        private EmbeddingModelDto? MapModelToDto(EmbeddingModel? model)
        {
            if (model == null) return null;
            return new EmbeddingModelDto
            {
                ModelId = model.ModelId,
                ModelName = model.ModelName,
                Provider = model.Provider
            };
        }

        private EmbeddingModel? MapModelToEntity(EmbeddingModelDto? dto)
        {
            if (dto == null) return null;
            return new EmbeddingModel
            {
                ModelId = dto.ModelId,
                ModelName = dto.ModelName,
                Provider = dto.Provider
            };
        }

        private DocumentIndexDto? MapIndexToDto(DocumentIndex? index)
        {
            if (index == null) return null;
            return new DocumentIndexDto
            {
                IndexId = index.IndexId,
                DocumentId = index.DocumentId,
                ModelId = index.ModelId,
                StrategyId = index.StrategyId,
                ChunkSize = index.ChunkSize,
                ChunkOverlap = index.ChunkOverlap,
                CreatedAt = index.CreatedAt,
                Document = MapDocumentToDto(index.Document),
                Model = MapModelToDto(index.Model),
                Strategy = MapStrategyToDto(index.Strategy)
            };
        }

        private DocumentIndex? MapIndexToEntity(DocumentIndexDto? dto)
        {
            if (dto == null) return null;
            return new DocumentIndex
            {
                IndexId = dto.IndexId,
                DocumentId = dto.DocumentId,
                ModelId = dto.ModelId,
                StrategyId = dto.StrategyId,
                ChunkSize = dto.ChunkSize,
                ChunkOverlap = dto.ChunkOverlap,
                CreatedAt = dto.CreatedAt
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
                Index = MapIndexToDto(chunk.Index)
            };
        }

        private DocumentChunk? MapChunkToEntity(DocumentChunkDto? dto)
        {
            if (dto == null) return null;
            return new DocumentChunk
            {
                ChunkId = dto.ChunkId,
                IndexId = dto.IndexId,
                ChunkOrder = dto.ChunkOrder,
                Content = dto.Content,
                PageNumber = dto.PageNumber,
                TokenCount = dto.TokenCount,
                VectorStoreKey = dto.VectorStoreKey,
                EmbeddingVector = dto.EmbeddingVector,
                HasEmbedding = dto.HasEmbedding
            };
        }
        #endregion

        #region Subjects
        public async Task<IEnumerable<SubjectDto>> GetAllSubjectsAsync()
        {
            var subjects = await _subjectRepo.GetAllAsync(
                orderBy: q => q.OrderBy(s => s.SubjectCode),
                includeProperties: "SubjectTeachers.User"
            );
            return subjects.Select(s => MapSubjectToDto(s)!).ToList();
        }

        public async Task<SubjectDto?> GetSubjectByIdAsync(int id)
        {
            var subject = await _subjectRepo.GetFirstOrDefaultAsync(
                filter: s => s.SubjectId == id,
                includeProperties: "SubjectTeachers.User"
            );
            return MapSubjectToDto(subject);
        }

        public async Task<SubjectDto> CreateSubjectAsync(SubjectDto subjectDto)
        {
            var subject = MapSubjectToEntity(subjectDto)!;
            await _subjectRepo.AddAsync(subject);
            await _subjectRepo.SaveAsync();
            return MapSubjectToDto(subject)!;
        }

        public async Task UpdateSubjectAsync(SubjectDto subjectDto)
        {
            var existing = await _subjectRepo.GetByIdAsync(subjectDto.SubjectId);
            if (existing != null)
            {
                existing.SubjectCode = subjectDto.SubjectCode;
                existing.SubjectName = subjectDto.SubjectName;
                _subjectRepo.Update(existing);
                await _subjectRepo.SaveAsync();
            }
        }

        public async Task DeleteSubjectAsync(int id)
        {
            await _subjectRepo.DeleteByIdAsync(id);
            await _subjectRepo.SaveAsync();
        }

        public async Task<bool> IsUserAssignedToSubjectAsync(int userId, int subjectId)
        {
            var relation = await _subjectTeacherRepo.GetFirstOrDefaultAsync(
                st => st.SubjectId == subjectId && st.UserId == userId
            );
            return relation != null;
        }

        public async Task<bool> IsUserSubjectHeadAsync(int userId, int subjectId)
        {
            var relation = await _subjectTeacherRepo.GetFirstOrDefaultAsync(
                st => st.SubjectId == subjectId && st.UserId == userId
            );
            return relation != null && relation.IsSubjectHead;
        }

        public async Task<bool> IsUserSubjectHeadForChapterAsync(int userId, int chapterId)
        {
            var chapter = await _chapterRepo.GetByIdAsync(chapterId);
            if (chapter == null) return false;
            return await IsUserSubjectHeadAsync(userId, chapter.SubjectId);
        }

        public async Task AssignTeachersToSubjectAsync(int subjectId, List<int> teacherIds, int? headTeacherId)
        {
            // Delete old relations
            var existing = await _subjectTeacherRepo.GetAllAsync(st => st.SubjectId == subjectId);
            foreach (var rel in existing)
            {
                _subjectTeacherRepo.Delete(rel);
            }
            await _subjectTeacherRepo.SaveAsync();

            // Add new relations
            if (teacherIds != null)
            {
                foreach (var tId in teacherIds.Distinct())
                {
                    var isHead = (tId == headTeacherId);
                    var rel = new SubjectTeacher
                    {
                        SubjectId = subjectId,
                        UserId = tId,
                        IsSubjectHead = isHead
                    };
                    await _subjectTeacherRepo.AddAsync(rel);
                }
                await _subjectTeacherRepo.SaveAsync();
            }
        }

        public async Task<IEnumerable<UserDto>> GetTeachersBySubjectIdAsync(int subjectId)
        {
            var relations = await _subjectTeacherRepo.GetAllAsync(
                filter: st => st.SubjectId == subjectId,
                includeProperties: "User"
            );
            return relations.Select(st => new UserDto
            {
                UserId = st.User.UserId,
                FullName = st.User.FullName,
                Email = st.User.Email,
                Role = st.User.Role
            }).ToList();
        }
        #endregion

        #region Chapters
        public async Task<IEnumerable<ChapterDto>> GetChaptersBySubjectIdAsync(int subjectId)
        {
            var chapters = await _chapterRepo.GetAllAsync(
                filter: c => c.SubjectId == subjectId,
                orderBy: q => q.OrderBy(c => c.ChapterNumber)
            );
            return chapters.Select(c => MapChapterToDto(c)!).ToList();
        }

        public async Task<ChapterDto?> GetChapterByIdAsync(int id)
        {
            var chapter = await _chapterRepo.GetByIdAsync(id);
            return MapChapterToDto(chapter);
        }

        public async Task<ChapterDto> CreateChapterAsync(ChapterDto chapterDto)
        {
            var chapter = MapChapterToEntity(chapterDto)!;
            await _chapterRepo.AddAsync(chapter);
            await _chapterRepo.SaveAsync();
            return MapChapterToDto(chapter)!;
        }

        public async Task UpdateChapterAsync(ChapterDto chapterDto)
        {
            var existing = await _chapterRepo.GetByIdAsync(chapterDto.ChapterId);
            if (existing != null)
            {
                existing.ChapterNumber = chapterDto.ChapterNumber;
                existing.ChapterName = chapterDto.ChapterName;
                _chapterRepo.Update(existing);
                await _chapterRepo.SaveAsync();
            }
        }

        public async Task DeleteChapterAsync(int id)
        {
            await _chapterRepo.DeleteByIdAsync(id);
            await _chapterRepo.SaveAsync();
        }
        #endregion

        #region Documents
        public async Task<IEnumerable<DocumentDto>> GetDocumentsByChapterIdAsync(int chapterId)
        {
            var docs = await _documentRepo.GetAllAsync(
                filter: d => d.ChapterId == chapterId,
                includeProperties: "UploadedByNavigation"
            );
            return docs.Select(d => MapDocumentToDto(d)!).ToList();
        }

        public async Task<IEnumerable<DocumentDto>> GetIndexedDocumentsAsync(int subjectId)
        {
            var docs = await _documentRepo.GetAllAsync(
                filter: d => d.Chapter.SubjectId == subjectId && d.Status == "Indexed",
                includeProperties: "Chapter"
            );
            return docs.Select(d => MapDocumentToDto(d)!).ToList();
        }

        public async Task<string> GetEmbeddingStatusAsync(int documentId)
        {
            var chunks = (await _chunkRepo.GetAllAsync(
                filter: c => c.Index.DocumentId == documentId,
                includeProperties: "Index"))
                .ToList();

            if (!chunks.Any()) return "NotIndexed";
            if (chunks.All(c => c.HasEmbedding)) return "VectorReady";
            return "NeedsReIndex";
        }

        public async Task<DocumentDto?> GetDocumentByIdAsync(int id)
        {
            var doc = await _documentRepo.GetFirstOrDefaultAsync(
                filter: d => d.DocumentId == id,
                includeProperties: "Chapter,UploadedByNavigation"
            );
            return MapDocumentToDto(doc);
        }

        public async Task<DocumentDto> UploadDocumentAsync(DocumentDto documentDto, string textContent)
        {
            var document = MapDocumentToEntity(documentDto)!;
            document.Status = "Pending";
            await _documentRepo.AddAsync(document);
            await _documentRepo.SaveAsync();

            // Save the extracted text content to a local storage file
            // Let's create an uploads directory inside the workspace PresentationLayer/wwwroot/uploads
            string uploadsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "uploads", "documents");
            if (!Directory.Exists(uploadsDir))
            {
                Directory.CreateDirectory(uploadsDir);
            }

            string textFilePath = Path.Combine(uploadsDir, $"{document.DocumentId}_content.txt");
            await File.WriteAllTextAsync(textFilePath, textContent, Encoding.UTF8);

            return MapDocumentToDto(document)!;
        }

        public async Task DeleteDocumentAsync(int id)
        {
            var doc = await _documentRepo.GetByIdAsync(id);
            if (doc != null)
            {
                // Delete physical file if exists
                string uploadsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "uploads", "documents");
                string textFilePath = Path.Combine(uploadsDir, $"{doc.DocumentId}_content.txt");
                if (File.Exists(textFilePath))
                {
                    try { File.Delete(textFilePath); } catch {}
                }

                if (File.Exists(doc.FilePath))
                {
                    try { File.Delete(doc.FilePath); } catch {}
                }

                _documentRepo.Delete(doc);
                await _documentRepo.SaveAsync();
            }
        }
        #endregion

        #region Chunking Strategies & Embedding Models
        public async Task<IEnumerable<ChunkingStrategyDto>> GetAllChunkingStrategiesAsync()
        {
            var strategies = await _strategyRepo.GetAllAsync();
            return strategies.Select(s => MapStrategyToDto(s)!).ToList();
        }

        public async Task<IEnumerable<EmbeddingModelDto>> GetAllEmbeddingModelsAsync()
        {
            var models = await _modelRepo.GetAllAsync();
            return models.Select(m => MapModelToDto(m)!).ToList();
        }
        #endregion

        #region Indexing & Ingestion
        public async Task<DocumentIndexDto> IndexDocumentAsync(int documentId, int modelId, int strategyId, int chunkSize, int chunkOverlap)
        {
            var doc = await _documentRepo.GetByIdAsync(documentId);
            if (doc == null) throw new ArgumentException("Tài liệu không tồn tại.");
            var embeddingModel = await _modelRepo.GetByIdAsync(modelId);
            if (embeddingModel == null) throw new ArgumentException("Embedding model không tồn tại.");

            _logger.LogInformation(
                "Start indexing DocumentId={DocumentId}, Model={ModelName}, Provider={Provider}, StrategyId={StrategyId}, ChunkSize={ChunkSize}, ChunkOverlap={ChunkOverlap}",
                documentId,
                embeddingModel.ModelName,
                embeddingModel.Provider,
                strategyId,
                chunkSize,
                chunkOverlap);

            doc.Status = "Processing";
            _documentRepo.Update(doc);
            await _documentRepo.SaveAsync();

            try
            {
                // Read text content
                string uploadsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "uploads", "documents");
                string textFilePath = Path.Combine(uploadsDir, $"{documentId}_content.txt");
                string contentText = "";
                if (File.Exists(textFilePath))
                {
                    contentText = await File.ReadAllTextAsync(textFilePath, Encoding.UTF8);
                }
                else
                {
                    throw new FileNotFoundException("Không tìm thấy tệp nội dung trích xuất.");
                }
                _logger.LogInformation("Read extracted text for DocumentId={DocumentId}. Length={Length}", documentId, contentText.Length);

                // Check if this index configuration already exists
                var existingIndexes = await _indexRepo.GetAllAsync(
                    filter: idx => idx.DocumentId == documentId &&
                                   idx.ModelId == modelId &&
                                   idx.StrategyId == strategyId &&
                                   idx.ChunkSize == chunkSize &&
                                   idx.ChunkOverlap == chunkOverlap
                );

                DocumentIndex indexRecord;
                if (existingIndexes.Any())
                {
                    indexRecord = existingIndexes.First();
                    _logger.LogInformation("Reusing existing index IndexId={IndexId} for DocumentId={DocumentId}", indexRecord.IndexId, documentId);
                    // Clear old chunks for this index
                    var oldChunks = (await _chunkRepo.GetAllAsync(c => c.IndexId == indexRecord.IndexId)).ToList();
                    var oldChunkIds = oldChunks.Select(c => c.ChunkId).ToList();
                    if (oldChunkIds.Count > 0)
                    {
                        var oldCitations = await _citationRepo.GetAllAsync(c => oldChunkIds.Contains(c.ChunkId));
                        foreach (var citation in oldCitations)
                        {
                            _citationRepo.Delete(citation);
                        }
                        await _citationRepo.SaveAsync();
                    }

                    foreach (var chunk in oldChunks)
                    {
                        _chunkRepo.Delete(chunk);
                    }
                    await _chunkRepo.SaveAsync();
                }
                else
                {
                    indexRecord = new DocumentIndex
                    {
                        DocumentId = documentId,
                        ModelId = modelId,
                        StrategyId = strategyId,
                        ChunkSize = chunkSize,
                        ChunkOverlap = chunkOverlap,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _indexRepo.AddAsync(indexRecord);
                    await _indexRepo.SaveAsync();
                    _logger.LogInformation("Created new index IndexId={IndexId} for DocumentId={DocumentId}", indexRecord.IndexId, documentId);
                }

                // Run chunking strategy
                List<string> chunks = PerformChunking(contentText, strategyId, chunkSize, chunkOverlap);
                _logger.LogInformation("Chunking completed for DocumentId={DocumentId}. ChunkCount={ChunkCount}", documentId, chunks.Count);
                if (chunks.Count == 0)
                {
                    // Fallback chunk instead of throwing exception to avoid UI crashing on empty/scanned files
                    chunks = new List<string>
                    {
                        $"[Hệ thống RAG - Cảnh báo tài liệu rỗng hoặc quét ảnh] Tài liệu có tiêu đề \"{doc.Title}\" (Tệp: {doc.FileName}) không thể trích xuất lớp chữ kỹ thuật số. " +
                        $"Có thể tệp này chỉ chứa hình ảnh quét, công thức dạng vẽ hình hoặc không chứa nội dung chữ tương thích. " +
                        $"Hệ thống đã tự động ghi nhận học liệu này để tránh gián đoạn. Vui lòng tải tài liệu dạng văn bản trực tiếp nếu muốn hỏi đáp."
                    };
                    _logger.LogWarning("Zero chunks generated for DocumentId={DocumentId}. Created fallback warning chunk.", documentId);
                }

                // Insert DocumentChunks
                int order = 1;
                foreach (var chunkText in chunks)
                {
                    if (string.IsNullOrWhiteSpace(chunkText)) continue;

                    var embedding = await GenerateEmbeddingWithFallbackAsync(embeddingModel, chunkText, documentId, order);

                    // Keep vector key deterministic and short enough for DB constraints.
                    string vectorKey = $"vec_{indexRecord.IndexId}_{order}";
                    
                    var chunk = new DocumentChunk
                    {
                        IndexId = indexRecord.IndexId,
                        ChunkOrder = order,
                        Content = chunkText,
                        PageNumber = (order / 3) + 1, // Simulated page number mapping
                        TokenCount = chunkText.Length / 4, // Rough approximation
                        VectorStoreKey = vectorKey,
                        EmbeddingVector = embedding.Length > 0 ? SerializeVector(embedding) : null,
                        HasEmbedding = embedding.Length > 0
                    };

                    await _chunkRepo.AddAsync(chunk);
                    order++;
                }

                await _chunkRepo.SaveAsync();
                _logger.LogInformation("Saved {ChunkCount} chunks for IndexId={IndexId}", order - 1, indexRecord.IndexId);

                // Update document status
                doc.Status = "Indexed";
                doc.TotalPages = (order / 3) + 1;
                _documentRepo.Update(doc);
                await _documentRepo.SaveAsync();
                _logger.LogInformation("Indexing completed successfully for DocumentId={DocumentId}", documentId);

                return MapIndexToDto(indexRecord)!;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Indexing failed for DocumentId={DocumentId}. Details: {Details}", documentId, BuildExceptionDetails(ex));
                doc.Status = "Failed";
                _documentRepo.Update(doc);
                await _documentRepo.SaveAsync();
                throw;
            }
        }

        public async Task<IEnumerable<DocumentIndexDto>> GetIndexesByDocumentIdAsync(int documentId)
        {
            var indexes = await _indexRepo.GetAllAsync(
                filter: idx => idx.DocumentId == documentId,
                includeProperties: "Model,Strategy"
            );
            return indexes.Select(idx => MapIndexToDto(idx)!).ToList();
        }

        public async Task<IEnumerable<DocumentChunkDto>> GetChunksByIndexIdAsync(int indexId)
        {
            var chunks = await _chunkRepo.GetAllAsync(
                filter: c => c.IndexId == indexId,
                orderBy: q => q.OrderBy(c => c.ChunkOrder)
            );
            return chunks.Select(c => MapChunkToDto(c)!).ToList();
        }

        private List<string> PerformChunking(string text, int strategyId, int chunkSize, int chunkOverlap)
        {
            if (string.IsNullOrEmpty(text)) return new List<string>();
            if (chunkSize <= 0) chunkSize = 500;
            if (chunkOverlap < 0) chunkOverlap = 100;
            if (chunkOverlap >= chunkSize) chunkOverlap = chunkSize / 5;

            // Factory pattern — sử dụng các chunker class thật thay vì switch-case hardcode
            IChunkingStrategy chunker = strategyId switch
            {
                1 => new FixedSizeChunker(chunkSize, chunkOverlap),
                2 => new ParagraphChunker(),
                3 => new SentenceChunker(),
                _ => new RecursiveChunker(chunkSize) // Strategy 4 hoặc mặc định
            };

            return chunker.Chunk(text)
                          .Select(c => c.Trim())
                          .Where(c => c.Length > 0)
                          .ToList();
        }

        private async Task<float[]> GenerateEmbeddingWithFallbackAsync(
            EmbeddingModel model,
            string chunkText,
            int documentId,
            int chunkOrder)
        {
            try
            {
                // ── Chọn đúng provider theo tên model từ DB ──
                var provider = _embeddingFactory.GetProvider(model.ModelName ?? string.Empty);

                _logger.LogInformation(
                    "[EMBED] Using provider={Provider}, model={Model} for DocumentId={DocumentId}, Chunk#{Order}",
                    provider.ProviderName, provider.ModelName, documentId, chunkOrder);

                var vector = await provider.GetEmbeddingAsync(chunkText);
                if (vector.Length > 0)
                    return vector;

                _logger.LogWarning(
                    "[EMBED] Provider '{Provider}' returned empty vector. DocumentId={DocumentId}, Chunk#{Order}",
                    provider.ProviderName, documentId, chunkOrder);
                return Array.Empty<float>();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[EMBED] Embedding error for model={ModelName}, DocumentId={DocumentId}, Chunk#{Order}. Details: {Details}",
                    model.ModelName, documentId, chunkOrder, BuildExceptionDetails(ex));
                return Array.Empty<float>();
            }
        }

        private string SerializeVector(float[] vector)
        {
            return JsonSerializer.Serialize(vector);
        }

        private float[] DeserializeVector(string? json)
        {
            if (string.IsNullOrEmpty(json)) return Array.Empty<float>();
            return JsonSerializer.Deserialize<float[]>(json) ?? Array.Empty<float>();
        }

        private static string BuildExceptionDetails(Exception ex)
        {
            var messages = new List<string>();
            var current = ex;
            while (current != null)
            {
                messages.Add($"{current.GetType().Name}: {current.Message}");
                current = current.InnerException;
            }
            return string.Join(" | INNER => ", messages);
        }
        #endregion
    }
}
