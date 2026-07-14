using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using BusinessLayer.DTOs;
using BusinessLayer.Interfaces;

using Microsoft.AspNetCore.SignalR;
using PresentationLayer.Hubs;

namespace PresentationLayer.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly IChatService _chatService;
        private readonly IDocumentService _documentService;
        private readonly ILogger<ChatController> _logger;
        private readonly IHubContext<NewsHub> _hubContext;

        public ChatController(IChatService chatService, IDocumentService documentService, ILogger<ChatController> logger, IHubContext<NewsHub> hubContext)
        {
            _chatService = chatService;
            _documentService = documentService;
            _logger = logger;
            _hubContext = hubContext;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var subjects = await _documentService.GetAllSubjectsAsync();
            var strategies = await _documentService.GetAllChunkingStrategiesAsync();
            var models = await _documentService.GetAllEmbeddingModelsAsync();

            ViewBag.Strategies = strategies;
            ViewBag.EmbeddingModels = models;

            return View(subjects);
        }

        [HttpGet]
        public async Task<IActionResult> GetSubjects()
        {
            var subjects = await _documentService.GetAllSubjectsAsync();
            return Json(subjects.Select(s => new {
                s.SubjectId,
                s.SubjectCode,
                s.SubjectName
            }));
        }
        // --- AJAX Chat Session Operations ---

        [HttpGet]
        public async Task<IActionResult> GetSessions(int subjectId)
        {
            var userId = GetCurrentUserId();
            var sessions = await _chatService.GetSessionsAsync(userId, subjectId);
            return Json(sessions.Select(s => new {
                s.SessionId,
                s.Title,
                LastUpdatedAt = s.LastUpdatedAt?.AddHours(7).ToString("dd/MM/yyyy HH:mm")
            }));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSession(int subjectId, string title)
        {
            var userId = GetCurrentUserId();
            var name = string.IsNullOrEmpty(title) ? "Cuộc trò chuyện mới" : title;
            var session = await _chatService.CreateSessionAsync(userId, subjectId, name);
            return Json(new { success = true, sessionId = session.SessionId, title = session.Title });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RenameSession(Guid sessionId, string newTitle)
        {
            if (string.IsNullOrEmpty(newTitle)) return Json(new { success = false, message = "Tiêu đề không hợp lệ." });
            await _chatService.RenameSessionAsync(sessionId, newTitle);
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSession(Guid sessionId)
        {
            await _chatService.DeleteSessionAsync(sessionId);
            return Json(new { success = true });
        }

        // --- AJAX Chat Processing & Citation Extraction ---

        [HttpGet]
        public async Task<IActionResult> GetSessionHistory(Guid sessionId)
        {
            var history = await _chatService.GetChatHistoryAsync(sessionId);
            return Json(history.Select(h => new {
                h.HistoryId,
                h.UserMessage,
                h.BotResponse,
                Timestamp = h.Timestamp?.AddHours(7).ToString("HH:mm"),
                Citations = h.ChatCitations.Select(c => new {
                    c.CitationId,
                    c.ChunkId,
                    c.PageNumber,
                    Snippet = c.Snippet ?? "",
                    DocumentTitle = c.Chunk?.Index?.Document?.Title ?? "Tài liệu học tập"
                })
            }));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(Guid sessionId, string message, int modelId, int strategyId, int chunkSize, int chunkOverlap)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return Json(new { success = false, message = "Vui lòng nhập tin nhắn." });
            }

            try
            {
                var result = await _chatService.SendMessageWithScoresAsync(sessionId, message, modelId, strategyId, chunkSize, chunkOverlap);
                var historyRecord = result.History;
                
                return Json(new {
                    success = true,
                    historyId = historyRecord.HistoryId,
                    userMessage = historyRecord.UserMessage,
                    botResponse = historyRecord.BotResponse,
                    timestamp = historyRecord.Timestamp?.AddHours(7).ToString("HH:mm"),
                    citations = result.Citations.Select(x => new {
                        x.Citation.CitationId,
                        x.Citation.ChunkId,
                        x.Citation.PageNumber,
                        Snippet = x.Citation.Snippet ?? "",
                        DocumentTitle = x.Citation.Chunk?.Index?.Document?.Title ?? "Tài liệu học tập",
                        SimilarityScore = x.Score,
                        ScorePercent = (int)(x.Score * 100)
                    })
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi xử lý tin nhắn: {ex.Message}" });
            }
        }

        // =====================================================
        // STUDENT UPLOAD WORKFLOW
        // Upload PDF → Validate → Extract → Chunk → Embed → Index
        // =====================================================

        /// <summary>
        /// Lấy danh sách tài liệu đã được lập chỉ mục (Indexed) cho một môn học.
        /// Dùng để hiển thị sidebar document list cho Student.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetDocumentsBySubject(int subjectId)
        {
            if (subjectId <= 0) return Json(new List<object>());

            var chapters = await _documentService.GetChaptersBySubjectIdAsync(subjectId);
            var allDocs = new List<object>();

            foreach (var chapter in chapters)
            {
                var docs = await _documentService.GetDocumentsByChapterIdAsync(chapter.ChapterId);
                allDocs.AddRange(docs.Select(d => new
                {
                    d.DocumentId,
                    d.Title,
                    d.FileName,
                    d.FileType,
                    d.Status,
                    d.FileSize,
                    ChapterName = chapter.ChapterName,
                    IsIndexed = d.Status == "Indexed"
                }));
            }

            return Json(allDocs.OrderByDescending(d => ((dynamic)d).IsIndexed));
        }

        /// <summary>
        /// Student upload tài liệu cá nhân — full workflow:
        /// Validate → Extract text → Chunk → Embedding → Index → Trả về kết quả
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadStudentDocument(int subjectId, string title, IFormFile file)
        {
            _logger.LogInformation("UploadStudentDocument started. SubjectId={SubjectId}, Title={Title}, FileName={FileName}, FileSize={FileSize}",
                subjectId, title, file?.FileName, file?.Length);

            // BƯỚC 1: Validate file
            if (file == null || file.Length == 0)
                return Json(new { success = false, step = 1, message = "Vui lòng chọn tệp để tải lên." });

            if (string.IsNullOrWhiteSpace(title))
                return Json(new { success = false, step = 1, message = "Vui lòng nhập tiêu đề tài liệu." });

            if (subjectId <= 0)
                return Json(new { success = false, step = 1, message = "Vui lòng chọn môn học." });

            string ext = Path.GetExtension(file.FileName).ToLower();
            if (ext != ".txt" && ext != ".pdf" && ext != ".docx" && ext != ".pptx")
                return Json(new { success = false, step = 1, message = "Chỉ chấp nhận định dạng: .pdf, .docx, .pptx, .txt" });

            if (file.Length > 15 * 1024 * 1024) // 15MB limit
                return Json(new { success = false, step = 1, message = "Kích thước tệp không được vượt quá 15MB." });

            int userId = GetCurrentUserId();
            if (userId < 0)
            {
                _logger.LogWarning("UploadStudentDocument failed at step 1 because user id is invalid. Claim value={ClaimValue}",
                    User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                return Json(new { success = false, step = 1, message = "Không xác định được tài khoản đăng nhập. Vui lòng đăng nhập lại." });
            }

            // BƯỚC 2: Tìm chapter để gắn tài liệu
            ChapterDto? chapter;
            try
            {
                var chapters = await _documentService.GetChaptersBySubjectIdAsync(subjectId);
                chapter = chapters.FirstOrDefault();
                if (chapter == null)
                    return Json(new { success = false, step = 2, message = "Môn học này chưa có chương nào. Vui lòng liên hệ giảng viên để tạo cấu trúc bài học." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UploadStudentDocument failed at step 2. SubjectId={SubjectId}. Details: {Details}",
                    subjectId, BuildExceptionDetails(ex));
                return Json(new { success = false, step = 2, message = "Lỗi khi lấy chương học. Vui lòng thử lại." });
            }

            // BƯỚC 3: Trích xuất văn bản từ file
            string textContent;
            try
            {
                textContent = await ExtractTextFromFileAsync(file, ext);
                if (string.IsNullOrWhiteSpace(textContent))
                    return Json(new { success = false, step = 3, message = "Không thể trích xuất nội dung từ tệp. Tệp có thể bị rỗng hoặc được mã hóa." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UploadStudentDocument failed at step 3 (extract text). FileName={FileName}. Details: {Details}",
                    file.FileName, BuildExceptionDetails(ex));
                return Json(new { success = false, step = 3, message = $"Lỗi trích xuất văn bản: {ex.Message}" });
            }

            // Check duplicate filename in chapter
            try
            {
                var existingDocs = await _documentService.GetDocumentsByChapterIdAsync(chapter.ChapterId);
                var duplicate = existingDocs.FirstOrDefault(d => d.FileName.Equals(file.FileName, StringComparison.OrdinalIgnoreCase));
                if (duplicate != null)
                {
                    _logger.LogInformation("Duplicate student document detected: {FileName}. Deleting old document (Id={DocId}) first.", file.FileName, duplicate.DocumentId);
                    await _documentService.DeleteDocumentAsync(duplicate.DocumentId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check or delete duplicate student document: {Message}", ex.Message);
            }

            // BƯỚC 4 & 5: Lưu tài liệu + Chunking
            var doc = new DocumentDto
            {
                ChapterId  = chapter.ChapterId,
                Title      = title.Trim(),
                FileName   = file.FileName,
                FilePath   = file.FileName,
                FileType   = ext.TrimStart('.').ToUpper(),
                FileSize   = file.Length,
                UploadedBy = userId
            };

            try
            {
                await _documentService.UploadDocumentAsync(doc, textContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UploadStudentDocument failed at step 4 (save document). SubjectId={SubjectId}, Title={Title}. Details: {Details}",
                    subjectId, title, BuildExceptionDetails(ex));
                return Json(new { success = false, step = 4, message = $"Lỗi lưu tài liệu: {ex.Message}" });
            }

            // BƯỚC 6: Embedding + Lập chỉ mục (Index)
            try
            {
                var subject = await _documentService.GetSubjectByIdAsync(subjectId);
                int modelId = subject?.DefaultModelId ?? 0;
                int strategyId = subject?.DefaultStrategyId ?? 0;
                int chunkSize = subject?.DefaultChunkSize ?? 500;
                int chunkOverlap = subject?.DefaultChunkOverlap ?? 100;

                // Fallbacks if not configured
                if (modelId == 0 || strategyId == 0)
                {
                    var models     = await _documentService.GetAllEmbeddingModelsAsync();
                    var strategies = await _documentService.GetAllChunkingStrategiesAsync();
                    var model      = models.FirstOrDefault();
                    var strategy   = strategies.FirstOrDefault();
                    if (model != null && strategy != null)
                    {
                        modelId = model.ModelId;
                        strategyId = strategy.StrategyId;
                    }
                }

                if (modelId > 0 && strategyId > 0)
                {
                    await _documentService.IndexDocumentAsync(
                        doc.DocumentId,
                        modelId,
                        strategyId,
                        chunkSize,
                        chunkOverlap
                    );
                }
                else
                {
                    return Json(new { success = false, step = 6, message = "Hệ thống chưa cấu hình Embedding Model hoặc Chunking Strategy. Vui lòng liên hệ Admin." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UploadStudentDocument failed at step 6 (indexing/embedding). DocumentId={DocumentId}. Details: {Details}",
                    doc.DocumentId, BuildExceptionDetails(ex));
                return Json(new { success = false, step = 6, message = $"Lỗi trong quá trình lập chỉ mục RAG: {ex.Message}" });
            }

            // BƯỚC 7: Trả về thành công
            await _hubContext.Clients.All.SendAsync("ReceiveDocumentUpdate", chapter.ChapterId);

            return Json(new
            {
                success    = true,
                documentId = doc.DocumentId,
                fileName   = file.FileName,
                title      = doc.Title,
                fileType   = doc.FileType,
                message    = $"Hoàn tất! Tài liệu \"{doc.Title}\" đã sẵn sàng để truy vấn."
            });
        }

        // --- Helper: Trích xuất văn bản từ nhiều định dạng file ---
        private async Task<string> ExtractTextFromFileAsync(IFormFile file, string ext)
        {
            if (ext == ".txt")
            {
                using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
                return await reader.ReadToEndAsync();
            }
            else if (ext == ".pdf")
            {
                using var document = UglyToad.PdfPig.PdfDocument.Open(file.OpenReadStream());
                var sb = new StringBuilder();
                foreach (var page in document.GetPages())
                {
                    var words = page.GetWords();
                    if (words != null && words.Any())
                    {
                        sb.AppendLine(string.Join(" ", words.Select(w => w.Text)));
                    }
                    else
                    {
                        sb.AppendLine(page.Text);
                    }
                }
                return sb.ToString();
            }
            else if (ext == ".docx")
            {
                using var archive = new System.IO.Compression.ZipArchive(file.OpenReadStream());
                var entry = archive.GetEntry("word/document.xml");
                if (entry == null) return string.Empty;

                using var entryStream = entry.Open();
                var xmlDoc = System.Xml.Linq.XDocument.Load(entryStream);
                System.Xml.Linq.XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                var paragraphs = xmlDoc.Descendants(w + "p");
                var sb = new StringBuilder();
                foreach (var p in paragraphs)
                    sb.AppendLine(string.Concat(p.Descendants(w + "t").Select(t => t.Value)));
                return sb.ToString();
            }
            else // .pptx — trích xuất text từ các slide XML
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Tài liệu trình chiếu: {file.FileName}");
                try
                {
                    using var archive = new System.IO.Compression.ZipArchive(file.OpenReadStream());
                    var slideEntries = archive.Entries
                        .Where(e => e.FullName.StartsWith("ppt/slides/slide") && e.FullName.EndsWith(".xml"))
                        .OrderBy(e => e.FullName)
                        .ToList();

                    int slideNum = 1;
                    foreach (var slideEntry in slideEntries)
                    {
                        using var slideStream = slideEntry.Open();
                        var slideDoc = System.Xml.Linq.XDocument.Load(slideStream);
                        System.Xml.Linq.XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
                        var texts = slideDoc.Descendants(a + "t").Select(t => t.Value).Where(t => !string.IsNullOrWhiteSpace(t));
                        sb.AppendLine($"\n--- Slide {slideNum++} ---");
                        sb.AppendLine(string.Join(" ", texts));
                    }
                }
                catch
                {
                    sb.AppendLine("(Không thể trích xuất nội dung slide — định dạng PPTX không chuẩn)");
                }
                return sb.ToString();
            }
        }

        // --- Helper Methods ---
        private int GetCurrentUserId()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0";
            int.TryParse(userIdString, out int userId);
            return userId;
        }

        private static string BuildExceptionDetails(Exception ex)
        {
            var messages = new List<string>();
            Exception? current = ex;
            while (current != null)
            {
                messages.Add($"{current.GetType().Name}: {current.Message}");
                current = current.InnerException;
            }

            return string.Join(" | INNER => ", messages);
        }
    }
}
