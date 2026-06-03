using System.Collections.Generic;
using System.Threading.Tasks;
using BusinessLayer.DTOs;

namespace BusinessLayer.Interfaces
{
    public interface IDocumentService
    {
        // Subjects
        Task<IEnumerable<SubjectDto>> GetAllSubjectsAsync();
        Task<SubjectDto?> GetSubjectByIdAsync(int id);
        Task<SubjectDto> CreateSubjectAsync(SubjectDto subjectDto);
        Task UpdateSubjectAsync(SubjectDto subjectDto);
        Task DeleteSubjectAsync(int id);
        Task<bool> IsUserAssignedToSubjectAsync(int userId, int subjectId);
        Task<bool> IsUserSubjectHeadAsync(int userId, int subjectId);
        Task<bool> IsUserSubjectHeadForChapterAsync(int userId, int chapterId);
        Task AssignTeachersToSubjectAsync(int subjectId, List<int> teacherIds, int? headTeacherId);
        Task<IEnumerable<UserDto>> GetTeachersBySubjectIdAsync(int subjectId);

        // Chapters
        Task<IEnumerable<ChapterDto>> GetChaptersBySubjectIdAsync(int subjectId);
        Task<ChapterDto?> GetChapterByIdAsync(int id);
        Task<ChapterDto> CreateChapterAsync(ChapterDto chapterDto);
        Task UpdateChapterAsync(ChapterDto chapterDto);
        Task DeleteChapterAsync(int id);

        // Documents
        Task<IEnumerable<DocumentDto>> GetDocumentsByChapterIdAsync(int chapterId);
        Task<IEnumerable<DocumentDto>> GetIndexedDocumentsAsync(int subjectId);
        Task<DocumentDto?> GetDocumentByIdAsync(int id);
        Task<DocumentDto> UploadDocumentAsync(DocumentDto documentDto, string textContent);
        Task DeleteDocumentAsync(int id);
        Task<string> GetEmbeddingStatusAsync(int documentId);

        // Chunking Strategies & Embedding Models
        Task<IEnumerable<ChunkingStrategyDto>> GetAllChunkingStrategiesAsync();
        Task<IEnumerable<EmbeddingModelDto>> GetAllEmbeddingModelsAsync();

        // Indexing & Chunking
        Task<DocumentIndexDto> IndexDocumentAsync(int documentId, int modelId, int strategyId, int chunkSize, int chunkOverlap);
        Task<IEnumerable<DocumentIndexDto>> GetIndexesByDocumentIdAsync(int documentId);
        Task<IEnumerable<DocumentChunkDto>> GetChunksByIndexIdAsync(int indexId);
    }
}
