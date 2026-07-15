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
        private readonly IGenericRepository<ChatSession> _sessionRepo;
        private readonly IGenericRepository<ChatHistory> _historyRepo;
        private readonly IGenericRepository<TestSet> _testSetRepo;
        private readonly IGenericRepository<BenchmarkResult> _benchmarkResultRepo;
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
            IGenericRepository<ChatSession> sessionRepo,
            IGenericRepository<ChatHistory> historyRepo,
            IGenericRepository<TestSet> testSetRepo,
            IGenericRepository<BenchmarkResult> benchmarkResultRepo,
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
            _sessionRepo      = sessionRepo;
            _historyRepo      = historyRepo;
            _testSetRepo      = testSetRepo;
            _benchmarkResultRepo = benchmarkResultRepo;
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
                DefaultModelId = subject.DefaultModelId,
                DefaultStrategyId = subject.DefaultStrategyId,
                DefaultChunkSize = subject.DefaultChunkSize,
                DefaultChunkOverlap = subject.DefaultChunkOverlap,
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
                SubjectName = dto.SubjectName,
                DefaultModelId = dto.DefaultModelId,
                DefaultStrategyId = dto.DefaultStrategyId,
                DefaultChunkSize = dto.DefaultChunkSize,
                DefaultChunkOverlap = dto.DefaultChunkOverlap
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
                FileHash = doc.FileHash,
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
                CreatedAt = dto.CreatedAt,
                FileHash = dto.FileHash
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
            var subjects = await _subjectRepo.GetAllNoTrackingAsync(
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
                existing.DefaultModelId = subjectDto.DefaultModelId;
                existing.DefaultStrategyId = subjectDto.DefaultStrategyId;
                existing.DefaultChunkSize = subjectDto.DefaultChunkSize;
                existing.DefaultChunkOverlap = subjectDto.DefaultChunkOverlap;
                _subjectRepo.Update(existing);
                await _subjectRepo.SaveAsync();
            }
        }

        public async Task DeleteSubjectAsync(int id)
        {
            var chapters = await _chapterRepo.GetAllAsync(c => c.SubjectId == id);
            if (chapters.Any())
            {
                throw new InvalidOperationException("Môn học này đang chứa các chương học. Vui lòng xóa hết các chương học trước khi xóa môn học.");
            }

            // 1. Delete ChatSessions (and their histories + citations) for this subject
            var sessions = await _sessionRepo.GetAllAsync(s => s.SubjectId == id);
            foreach (var s in sessions)
            {
                var histories = await _historyRepo.GetAllAsync(h => h.SessionId == s.SessionId);
                foreach (var h in histories)
                {
                    var citations = await _citationRepo.GetAllAsync(cit => cit.HistoryId == h.HistoryId);
                    foreach (var cit in citations)
                    {
                        _citationRepo.Delete(cit);
                    }
                    await _citationRepo.SaveAsync();
                    _historyRepo.Delete(h);
                }
                await _historyRepo.SaveAsync();
                _sessionRepo.Delete(s);
            }
            await _sessionRepo.SaveAsync();

            // 2. Delete TestSets & BenchmarkResults for this subject
            var testSets = await _testSetRepo.GetAllAsync(ts => ts.SubjectId == id);
            foreach (var ts in testSets)
            {
                var results = await _benchmarkResultRepo.GetAllAsync(r => r.QuestionId == ts.QuestionId);
                foreach (var r in results)
                {
                    _benchmarkResultRepo.Delete(r);
                }
                await _benchmarkResultRepo.SaveAsync();
                _testSetRepo.Delete(ts);
            }
            await _testSetRepo.SaveAsync();

            // 3. Delete teacher assignments
            var assignments = await _subjectTeacherRepo.GetAllAsync(st => st.SubjectId == id);
            foreach (var assignment in assignments)
            {
                _subjectTeacherRepo.Delete(assignment);
            }
            await _subjectTeacherRepo.SaveAsync();

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
            var docs = await _documentRepo.GetAllAsync(d => d.ChapterId == id);
            if (docs.Any(d => d.Status != "Deleted"))
            {
                throw new InvalidOperationException("Chương này đang chứa tài liệu. Vui lòng xóa hết tài liệu trong chương trước khi xóa chương.");
            }

            // Clean up soft-deleted documents to prevent foreign key violations on Chapter delete
            foreach (var doc in docs)
            {
                // Delete physical files
                string uploadsDir = GetUploadsDirectory();
                string textFilePath = Path.Combine(uploadsDir, $"{doc.DocumentId}_content.txt");
                if (File.Exists(textFilePath))
                {
                    try { File.Delete(textFilePath); } catch {}
                }
                if (File.Exists(doc.FilePath))
                {
                    try { File.Delete(doc.FilePath); } catch {}
                }

                // Delete child entities
                var indexes = await _indexRepo.GetAllAsync(idx => idx.DocumentId == doc.DocumentId);
                foreach (var idx in indexes)
                {
                    var chunks = await _chunkRepo.GetAllAsync(c => c.IndexId == idx.IndexId);
                    foreach (var c in chunks)
                    {
                        var citations = await _citationRepo.GetAllAsync(cit => cit.ChunkId == c.ChunkId);
                        foreach (var cit in citations)
                        {
                            _citationRepo.Delete(cit);
                        }
                        await _citationRepo.SaveAsync();
                        _chunkRepo.Delete(c);
                    }
                    await _chunkRepo.SaveAsync();
                    _indexRepo.Delete(idx);
                }
                await _indexRepo.SaveAsync();

                _documentRepo.Delete(doc);
            }
            await _documentRepo.SaveAsync();

            await _chapterRepo.DeleteByIdAsync(id);
            await _chapterRepo.SaveAsync();
        }
        #endregion

        #region Documents
        public async Task<bool> IsDuplicateFileHashAsync(int subjectId, string fileHash)
        {
            var docs = await _documentRepo.GetAllAsync(d => d.FileHash == fileHash && d.Chapter.SubjectId == subjectId && d.Status != "Deleted", includeProperties: "Chapter");
            return docs.Any();
        }

        public async Task<IEnumerable<DocumentDto>> GetDocumentsByChapterIdAsync(int chapterId)
        {
            var docs = await _documentRepo.GetAllNoTrackingAsync(
                filter: d => d.ChapterId == chapterId && d.Status != "Deleted",
                includeProperties: "UploadedByNavigation"
            );
            return docs.Select(d => MapDocumentToDto(d)!).ToList();
        }

        public async Task<IEnumerable<DocumentDto>> GetIndexedDocumentsAsync(int subjectId)
        {
            var docs = await _documentRepo.GetAllNoTrackingAsync(
                filter: d => d.Chapter.SubjectId == subjectId && d.Status == "Indexed",
                includeProperties: "Chapter"
            );
            return docs.Select(d => MapDocumentToDto(d)!).ToList();
        }

        public async Task<string> GetEmbeddingStatusAsync(int documentId)
        {
            var chunks = (await _chunkRepo.GetAllNoTrackingAsync(
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

        private string GetUploadsDirectory()
        {
            string currentDir = Directory.GetCurrentDirectory();
            if (Directory.Exists(Path.Combine(currentDir, "wwwroot")))
            {
                return Path.Combine(currentDir, "wwwroot", "uploads", "documents");
            }
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "uploads", "documents");
        }

        public async Task<DocumentDto> UploadDocumentAsync(DocumentDto documentDto, string textContent)
        {
            var document = MapDocumentToEntity(documentDto)!;
            document.Status = "Pending";
            await _documentRepo.AddAsync(document);
            await _documentRepo.SaveAsync();

            // Save the extracted text content to a local storage file
            string uploadsDir = GetUploadsDirectory();
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
                string uploadsDir = GetUploadsDirectory();
                string textFilePath = Path.Combine(uploadsDir, $"{doc.DocumentId}_content.txt");
                if (File.Exists(textFilePath))
                {
                    try { File.Delete(textFilePath); } catch {}
                }
                else
                {
                    // Fallback delete
                    string fallbackDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "uploads", "documents");
                    string fallbackPath = Path.Combine(fallbackDir, $"{doc.DocumentId}_content.txt");
                    if (File.Exists(fallbackPath))
                    {
                        try { File.Delete(fallbackPath); } catch {}
                    }
                }

                if (File.Exists(doc.FilePath))
                {
                    try { File.Delete(doc.FilePath); } catch {}
                }

                // Soft delete by updating status
                doc.Status = "Deleted";
                _documentRepo.Update(doc);
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
                string uploadsDir = GetUploadsDirectory();
                string textFilePath = Path.Combine(uploadsDir, $"{documentId}_content.txt");
                string contentText = "";
                if (File.Exists(textFilePath))
                {
                    contentText = await File.ReadAllTextAsync(textFilePath, Encoding.UTF8);
                }
                else
                {
                    // Fallback to bin folder
                    string fallbackDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "uploads", "documents");
                    string fallbackPath = Path.Combine(fallbackDir, $"{documentId}_content.txt");
                    if (File.Exists(fallbackPath))
                    {
                        contentText = await File.ReadAllTextAsync(fallbackPath, Encoding.UTF8);
                    }
                    else
                    {
                        throw new FileNotFoundException("Không tìm thấy tệp nội dung trích xuất (Tệp có thể đã bị xóa khi rebuild dự án). Vui lòng xóa tài liệu này và tải lên lại.");
                    }
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

                // ── OPTIMIZED: Parallel embedding + batch DB insert ──
                var validChunks = chunks.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
                int totalChunks = validChunks.Count;
                _logger.LogInformation("Starting parallel embedding for {Count} chunks, DocumentId={DocumentId}", totalChunks, documentId);

                // Parallel embedding with controlled concurrency (max 3 concurrent API calls)
                var semaphore = new System.Threading.SemaphoreSlim(3);
                var embeddingTasks = new Task<(int Order, string Text, float[] Embedding)>[totalChunks];

                for (int i = 0; i < totalChunks; i++)
                {
                    int chunkOrder = i + 1;
                    string chunkText = validChunks[i];
                    embeddingTasks[i] = Task.Run(async () =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            var emb = await GenerateEmbeddingWithFallbackAsync(embeddingModel, chunkText, documentId, chunkOrder);
                            return (chunkOrder, chunkText, emb);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });
                }

                var embeddingResults = await Task.WhenAll(embeddingTasks);

                // Batch create all DocumentChunk entities
                var chunkEntities = new List<DocumentChunk>(totalChunks);
                foreach (var result in embeddingResults.OrderBy(r => r.Order))
                {
                    string vectorKey = $"vec_{indexRecord.IndexId}_{result.Order}";
                    // Fast word count: count spaces instead of Split (avoids allocating string arrays)
                    int wordCount = 1;
                    for (int ci = 0; ci < result.Text.Length; ci++)
                    {
                        char ch = result.Text[ci];
                        if (ch == ' ' || ch == '\n' || ch == '\r' || ch == '\t') wordCount++;
                    }
                    chunkEntities.Add(new DocumentChunk
                    {
                        IndexId = indexRecord.IndexId,
                        ChunkOrder = result.Order,
                        Content = result.Text,
                        PageNumber = (result.Order / 3) + 1,
                        TokenCount = (int)(wordCount * 1.3),
                        VectorStoreKey = vectorKey,
                        EmbeddingVector = result.Embedding.Length > 0 ? SerializeVector(result.Embedding) : null,
                        HasEmbedding = result.Embedding.Length > 0
                    });
                }

                // Save chunks to DB in batches of 20 to prevent SQL CommandTimeout
                const int dbBatchSize = 20;
                for (int i = 0; i < chunkEntities.Count; i += dbBatchSize)
                {
                    var batch = chunkEntities.Skip(i).Take(dbBatchSize).ToList();
                    await _chunkRepo.AddRangeAsync(batch);
                    await _chunkRepo.SaveAsync();
                    _logger.LogInformation("Batch saved {Count}/{Total} chunks for IndexId={IndexId}", Math.Min(i + dbBatchSize, chunkEntities.Count), chunkEntities.Count, indexRecord.IndexId);
                }

                // Update document status
                doc.Status = "Indexed";
                doc.TotalPages = (totalChunks / 3) + 1;
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
            var indexes = await _indexRepo.GetAllNoTrackingAsync(
                filter: idx => idx.DocumentId == documentId,
                includeProperties: "Model,Strategy"
            );
            return indexes.Select(idx => MapIndexToDto(idx)!).ToList();
        }

        public async Task<IEnumerable<DocumentChunkDto>> GetChunksByIndexIdAsync(int indexId)
        {
            var chunks = await _chunkRepo.GetAllNoTrackingAsync(
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

            var result = chunker.Chunk(text)
                                .Select(c => c.Trim())
                                .Where(c => c.Length > 0)
                                .ToList();

            // Fallback thông minh: nếu strategy không phải FixedSize mà chỉ ra được 1 chunk
            // nhưng text lại rất dài (> 2x chunkSize) → tự động dùng FixedSizeChunker
            // Thường gặp với PDF toán học, scan, text không có xuống dòng kép (\n\n)
            if (result.Count <= 1 && text.Length > chunkSize * 2 && strategyId != 1)
            {
                _logger.LogWarning(
                    "Strategy {StrategyId} produced only {Count} chunk(s) for text length {Length}. " +
                    "Falling back to FixedSizeChunker (size={ChunkSize}, overlap={Overlap}).",
                    strategyId, result.Count, text.Length, chunkSize, chunkOverlap);

                result = new FixedSizeChunker(chunkSize, chunkOverlap)
                    .Chunk(text)
                    .Select(c => c.Trim())
                    .Where(c => c.Length > 0)
                    .ToList();
            }

            return result;
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
