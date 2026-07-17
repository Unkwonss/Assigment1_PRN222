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
    [Authorize(Roles = "Teacher,Admin")]
    public class DocumentController : Controller
    {
        private readonly IDocumentService _documentService;
        private readonly IUserService _userService;
        private readonly ILogger<DocumentController> _logger;
        private readonly IHubContext<NewsHub> _hubContext;

        public DocumentController(IDocumentService documentService, IUserService userService, ILogger<DocumentController> logger, IHubContext<NewsHub> hubContext)
        {
            _documentService = documentService;
            _userService = userService;
            _logger = logger;
            _hubContext = hubContext;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            IEnumerable<SubjectDto> subjects;
            if (role == "Admin")
            {
                subjects = await _documentService.GetAllSubjectsAsync();
            }
            else // Teacher
            {
                int.TryParse(userIdString, out int userId);
                var allSubjects = await _documentService.GetAllSubjectsAsync();
                var assignedSubjects = new List<SubjectDto>();
                foreach (var s in allSubjects)
                {
                    if (await _documentService.IsUserAssignedToSubjectAsync(userId, s.SubjectId))
                    {
                        assignedSubjects.Add(s);
                    }
                }
                subjects = assignedSubjects;
            }

            var strategies = await _documentService.GetAllChunkingStrategiesAsync();
            var models = await _documentService.GetAllEmbeddingModelsAsync();
            
            // Get all teachers for Subject assignment
            var allUsers = await _userService.GetAllUsersAsync();
            var teachers = allUsers.Where(u => u.Role == "Teacher").ToList();

            ViewBag.Strategies = strategies;
            ViewBag.EmbeddingModels = models;
            ViewBag.Teachers = teachers;

            return View(subjects);
        }

        [HttpGet]
        public async Task<IActionResult> GetSubjects()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            IEnumerable<SubjectDto> subjects;
            if (role == "Admin")
            {
                subjects = await _documentService.GetAllSubjectsAsync();
            }
            else // Teacher
            {
                int.TryParse(userIdString, out int userId);
                var allSubjects = await _documentService.GetAllSubjectsAsync();
                var assignedSubjects = new List<SubjectDto>();
                foreach (var s in allSubjects)
                {
                    if (await _documentService.IsUserAssignedToSubjectAsync(userId, s.SubjectId))
                    {
                        assignedSubjects.Add(s);
                    }
                }
                subjects = assignedSubjects;
            }

            return Json(subjects.Select(s => new {
                s.SubjectId,
                s.SubjectCode,
                s.SubjectName,
                s.ManagedByUserId,
                s.ManagedByUserName,
                s.AssignedTeacherIds,
                s.DefaultModelId,
                s.DefaultStrategyId,
                s.DefaultChunkSize,
                s.DefaultChunkOverlap
            }));
        }

        // --- Subject CRUD Actions ---

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSubject(string subjectCode, string subjectName, int? managedByUserId, List<int> teacherIds, int? defaultModelId, int? defaultStrategyId, int? defaultChunkSize, int? defaultChunkOverlap)
        {
            if (string.IsNullOrEmpty(subjectCode) || string.IsNullOrEmpty(subjectName))
            {
                TempData["Error"] = "Vui lòng nhập đầy đủ mã và tên môn học.";
                return RedirectToAction("Index");
            }

            try
            {
                var subject = new SubjectDto 
                { 
                    SubjectCode = subjectCode, 
                    SubjectName = subjectName,
                    DefaultModelId = defaultModelId ?? 1,
                    DefaultStrategyId = defaultStrategyId ?? 2,
                    DefaultChunkSize = defaultChunkSize ?? 500,
                    DefaultChunkOverlap = defaultChunkOverlap ?? 100
                };
                var created = await _documentService.CreateSubjectAsync(subject);
                
                // Assign teachers and subject head
                var allIds = teacherIds ?? new List<int>();
                if (managedByUserId.HasValue && !allIds.Contains(managedByUserId.Value))
                {
                    allIds.Add(managedByUserId.Value);
                }
                await _documentService.AssignTeachersToSubjectAsync(created.SubjectId, allIds, managedByUserId);

                TempData["Success"] = "Thêm môn học thành công!";
                await _hubContext.Clients.All.SendAsync("ReceiveSubjectUpdate");
                await _hubContext.Clients.All.SendAsync("ReceiveSystemNotification", "Môn học mới", $"Môn học '{created.SubjectName}' đã được thêm mới thành công.", "success");
            }
            catch (ArgumentException ex)
            {
                TempData["Error"] = ex.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thêm môn học mới.");
                TempData["Error"] = "Đã xảy ra lỗi không xác định khi thêm môn học.";
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSubject(int subjectId, string subjectCode, string subjectName, int? managedByUserId, List<int> teacherIds, int? defaultModelId, int? defaultStrategyId, int? defaultChunkSize, int? defaultChunkOverlap)
        {
            if (string.IsNullOrEmpty(subjectCode) || string.IsNullOrEmpty(subjectName))
            {
                TempData["Error"] = "Thông tin môn học không hợp lệ.";
                return RedirectToAction("Index");
            }

            try
            {
                var subject = new SubjectDto 
                { 
                    SubjectId = subjectId, 
                    SubjectCode = subjectCode, 
                    SubjectName = subjectName,
                    DefaultModelId = defaultModelId ?? 1,
                    DefaultStrategyId = defaultStrategyId ?? 2,
                    DefaultChunkSize = defaultChunkSize ?? 500,
                    DefaultChunkOverlap = defaultChunkOverlap ?? 100
                };
                await _documentService.UpdateSubjectAsync(subject);

                // Assign teachers and subject head
                var allIds = teacherIds ?? new List<int>();
                if (managedByUserId.HasValue && !allIds.Contains(managedByUserId.Value))
                {
                    allIds.Add(managedByUserId.Value);
                }
                await _documentService.AssignTeachersToSubjectAsync(subjectId, allIds, managedByUserId);

                TempData["Success"] = "Cập nhật môn học thành công!";
                await _hubContext.Clients.All.SendAsync("ReceiveSubjectUpdate");
                await _hubContext.Clients.All.SendAsync("ReceiveSystemNotification", "Cập nhật môn học", $"Môn học '{subject.SubjectName}' đã được cập nhật thành công.", "info");
            }
            catch (ArgumentException ex)
            {
                TempData["Error"] = ex.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật môn học.");
                TempData["Error"] = "Đã xảy ra lỗi không xác định khi cập nhật môn học.";
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSubject(int subjectId)
        {
            try
            {
                await _documentService.DeleteSubjectAsync(subjectId);
                TempData["Success"] = "Xóa môn học thành công!";
                await _hubContext.Clients.All.SendAsync("ReceiveSubjectUpdate");
                await _hubContext.Clients.All.SendAsync("ReceiveSystemNotification", "Xóa môn học", "Một môn học đã được xóa khỏi hệ thống.", "warning");
            }
            catch (Exception)
            {
                TempData["Error"] = "Không thể xóa môn học này. Vui lòng kiểm tra lại các ràng buộc.";
            }
            return RedirectToAction("Index");
        }

        // --- Chapter CRUD Actions (AJAX friendly) ---

        [HttpGet]
        public async Task<IActionResult> GetChapters(int subjectId)
        {
            var chapters = await _documentService.GetChaptersBySubjectIdAsync(subjectId);
            return Json(chapters.Select(c => new { c.ChapterId, c.ChapterNumber, c.ChapterName }));
        }

        private async Task<(bool Success, string Message, int UserId)> CheckPermissionForSubjectAsync(int subjectId)
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            if (role == "Admin")
            {
                return (false, "Từ chối truy cập: Admin không được phép tạo chương hay tải lên tài liệu.", 0);
            }

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return (false, "Không thể xác định thông tin tài khoản đăng nhập.", 0);
            }

            bool isHead = await _documentService.IsUserSubjectHeadAsync(userId, subjectId);
            if (!isHead)
            {
                return (false, "Từ chối truy cập: Chỉ Trưởng bộ môn của môn học này mới được phép thao tác.", userId);
            }

            return (true, string.Empty, userId);
        }

        private async Task<(bool Success, string Message, int UserId)> CheckPermissionForChapterAsync(int chapterId)
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            if (role == "Admin")
            {
                return (false, "Từ chối truy cập: Admin không được phép tạo chương hay tải lên tài liệu.", 0);
            }

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return (false, "Không thể xác định thông tin tài khoản đăng nhập.", 0);
            }

            bool isHead = await _documentService.IsUserSubjectHeadForChapterAsync(userId, chapterId);
            if (!isHead)
            {
                return (false, "Từ chối truy cập: Chỉ Trưởng bộ môn của môn học này mới được phép thao tác.", userId);
            }

            return (true, string.Empty, userId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateChapter(int subjectId, int chapterNumber, string chapterName)
        {
            var auth = await CheckPermissionForSubjectAsync(subjectId);
            if (!auth.Success)
            {
                return Json(new { success = false, message = auth.Message });
            }

            if (string.IsNullOrEmpty(chapterName) || chapterNumber <= 0)
            {
                return Json(new { success = false, message = "Thông tin chương không hợp lệ." });
            }

            try
            {
                var chapter = new ChapterDto { SubjectId = subjectId, ChapterNumber = chapterNumber, ChapterName = chapterName };
                await _documentService.CreateChapterAsync(chapter);
                await _hubContext.Clients.All.SendAsync("ReceiveChapterUpdate", subjectId);
                await _hubContext.Clients.All.SendAsync("ReceiveSystemNotification", "Chương học mới", $"Chương '{chapter.ChapterNumber}: {chapter.ChapterName}' đã được tạo thành công.", "success");
                return Json(new { success = true, message = "Thêm chương mới thành công!" });
            }
            catch (ArgumentException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo chương mới.");
                return Json(new { success = false, message = "Đã xảy ra lỗi không xác định." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditChapter(int chapterId, int chapterNumber, string chapterName)
        {
            var auth = await CheckPermissionForChapterAsync(chapterId);
            if (!auth.Success)
            {
                return Json(new { success = false, message = auth.Message });
            }

            if (string.IsNullOrEmpty(chapterName) || chapterNumber <= 0)
            {
                return Json(new { success = false, message = "Thông tin chỉnh sửa không hợp lệ." });
            }

            try
            {
                var existing = await _documentService.GetChapterByIdAsync(chapterId);
                if (existing == null) return Json(new { success = false, message = "Chương không tồn tại." });

                existing.ChapterNumber = chapterNumber;
                existing.ChapterName = chapterName;
                await _documentService.UpdateChapterAsync(existing);
                await _hubContext.Clients.All.SendAsync("ReceiveChapterUpdate", existing.SubjectId);
                await _hubContext.Clients.All.SendAsync("ReceiveSystemNotification", "Cập nhật chương", $"Chương '{existing.ChapterNumber}: {existing.ChapterName}' đã được cập nhật.", "info");
                return Json(new { success = true, message = "Cập nhật chương thành công!" });
            }
            catch (ArgumentException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật chương.");
                return Json(new { success = false, message = "Đã xảy ra lỗi không xác định." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteChapter(int chapterId)
        {
            var auth = await CheckPermissionForChapterAsync(chapterId);
            if (!auth.Success)
            {
                return Json(new { success = false, message = auth.Message });
            }

            try
            {
                var existing = await _documentService.GetChapterByIdAsync(chapterId);
                int subjectId = existing?.SubjectId ?? 0;
                string chapName = existing != null ? $"Chương '{existing.ChapterNumber}: {existing.ChapterName}'" : "Một chương học";
                await _documentService.DeleteChapterAsync(chapterId);
                if (subjectId > 0)
                {
                    await _hubContext.Clients.All.SendAsync("ReceiveChapterUpdate", subjectId);
                }
                await _hubContext.Clients.All.SendAsync("ReceiveSystemNotification", "Xóa chương học", $"{chapName} đã được xóa khỏi môn học.", "warning");
                return Json(new { success = true, message = "Xóa chương thành công!" });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "Lỗi khi xóa chương." });
            }
        }

        // --- Document CRUD Actions ---

        [HttpGet]
        public async Task<IActionResult> GetDocuments(int chapterId)
        {
            var docs = await _documentService.GetDocumentsByChapterIdAsync(chapterId);
            var result = new List<object>();
            foreach (var d in docs)
            {
                var embeddingStatus = await _documentService.GetEmbeddingStatusAsync(d.DocumentId);
                result.Add(new
                {
                    d.DocumentId,
                    d.Title,
                    d.FileName,
                    d.FileType,
                    FileSize = FormatBytes(d.FileSize),
                    d.TotalPages,
                    d.Status,
                    UploadedBy = d.UploadedByNavigation?.FullName ?? "Giảng viên",
                    EmbeddingStatus = embeddingStatus
                });
            }
            return Json(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadDocument(int chapterId, string title, IFormFile file)
        {
            _logger.LogInformation("UploadDocument called. ChapterId={ChapterId}, Title={Title}, FileName={FileName}, FileSize={FileSize}",
                chapterId, title, file?.FileName, file?.Length);

            var auth = await CheckPermissionForChapterAsync(chapterId);
            if (!auth.Success)
            {
                return Json(new { success = false, message = auth.Message });
            }
            int userId = auth.UserId;

            if (file == null || file.Length == 0 || string.IsNullOrEmpty(title))
            {
                return Json(new { success = false, message = "Vui lòng nhập tiêu đề và chọn tệp hợp lệ." });
            }

            string ext = Path.GetExtension(file.FileName).ToLower();
            if (ext != ".txt" && ext != ".pdf" && ext != ".docx" && ext != ".pptx")
            {
                return Json(new { success = false, message = "Chỉ chấp nhận các tệp định dạng .txt, .pdf, .docx, .pptx." });
            }

            // ── OPTIMIZED: Buffer file once, reuse for hash + extraction ──
            using var fileBuffer = new MemoryStream();
            await file.CopyToAsync(fileBuffer);
            fileBuffer.Position = 0;

            // Calculate FileHash (SHA256) from buffer
            string fileHash = "";
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hashBytes = await sha256.ComputeHashAsync(fileBuffer);
                fileHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }

            var chapter = await _documentService.GetChapterByIdAsync(chapterId);
            if (chapter == null) return Json(new { success = false, message = "Không tìm thấy chương học." });

            bool isDuplicate = await _documentService.IsDuplicateFileHashAsync(chapter.SubjectId, fileHash);
            if (isDuplicate)
            {
                return Json(new { success = false, message = "Tài liệu này đã tồn tại trong môn học, vui lòng không tải lên lại!" });
            }

            // Extract content from the same buffered stream (no second file read)
            fileBuffer.Position = 0;
            string textContent = "";
            if (ext == ".txt")
            {
                using (var reader = new StreamReader(fileBuffer, Encoding.UTF8, leaveOpen: true))
                {
                    textContent = await reader.ReadToEndAsync();
                }
            }
            else if (ext == ".pdf")
            {
                textContent = ExtractTextFromPdf(fileBuffer);
            }
            else if (ext == ".docx")
            {
                textContent = ExtractTextFromDocx(fileBuffer);
            }
            else if (ext == ".pptx")
            {
                textContent = ExtractTextFromPptx(fileBuffer, file.FileName);
            }
            else
            {
                // Simulate robust text extraction based on curriculum context
                textContent = GenerateCurriculumSimulationText(title);
            }

            // Check duplicate filename in chapter
            var existingDocs = await _documentService.GetDocumentsByChapterIdAsync(chapterId);
            var duplicate = existingDocs.FirstOrDefault(d => d.FileName.Equals(file.FileName, StringComparison.OrdinalIgnoreCase));
            if (duplicate != null)
            {
                return Json(new { success = false, message = $"Một tài liệu khác mang tên '{file.FileName}' đã tồn tại trong chương này. Vui lòng đổi tên tệp trước khi tải lên để tránh nhầm lẫn!" });
            }

            var doc = new DocumentDto
            {
                ChapterId = chapterId,
                Title = title,
                FileName = file.FileName,
                FilePath = file.FileName, // Stored as metadata
                FileType = ext.Substring(1).ToUpper(),
                FileSize = file.Length,
                UploadedBy = userId,
                FileHash = fileHash
            };

            try
            {
                var uploaded = await _documentService.UploadDocumentAsync(doc, textContent);

                // Auto index if default setting exists
                var subject = chapter != null ? await _documentService.GetSubjectByIdAsync(chapter.SubjectId) : null;
                bool wasAutoIndexed = false;
                if (subject != null && subject.DefaultModelId.HasValue && subject.DefaultStrategyId.HasValue)
                {
                    _logger.LogInformation("Auto-indexing uploaded document {DocId} with Model={ModelId}, Strategy={StrategyId}",
                        uploaded.DocumentId, subject.DefaultModelId.Value, subject.DefaultStrategyId.Value);
                    await _documentService.IndexDocumentAsync(
                        uploaded.DocumentId,
                        subject.DefaultModelId.Value,
                        subject.DefaultStrategyId.Value,
                        subject.DefaultChunkSize ?? 500,
                        subject.DefaultChunkOverlap ?? 100
                    );
                    wasAutoIndexed = true;
                }

                await _hubContext.Clients.All.SendAsync("ReceiveDocumentUpdate", chapterId);
                await _hubContext.Clients.All.SendAsync("ReceiveSystemNotification", "Tài liệu mới", $"Tài liệu '{title}' đã được tải lên thành công.", "success");
                
                string msg = wasAutoIndexed 
                    ? "Tải lên và tự động lập chỉ mục tài liệu thành công!" 
                    : "Tải lên tài liệu thành công!";
                return Json(new { success = true, message = msg });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UploadDocument failed. ChapterId={ChapterId}, Title={Title}. Details: {Details}",
                    chapterId, title, BuildExceptionDetails(ex));
                return Json(new { success = false, message = $"Lỗi lưu tài liệu: {ex.Message}" });
            }
        }

        private string ExtractTextFromPptx(Stream pptxStream, string fileName)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Tài liệu trình chiếu: {fileName}");
            try
            {
                using var archive = new System.IO.Compression.ZipArchive(pptxStream);
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
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"(Không thể trích xuất nội dung slide: {ex.Message} — dùng giả lập thay thế)\n" + GenerateCurriculumSimulationText(fileName);
            }
        }

        private string ExtractTextFromPdf(Stream pdfStream)
        {
            try
            {
                using (var document = UglyToad.PdfPig.PdfDocument.Open(pdfStream))
                {
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
            }
            catch (Exception ex)
            {
                return $"[Lỗi trích xuất PDF: {ex.Message}]";
            }
        }

        private string ExtractTextFromDocx(Stream docxStream)
        {
            try
            {
                using (var archive = new System.IO.Compression.ZipArchive(docxStream))
                {
                    var entry = archive.GetEntry("word/document.xml");
                    if (entry == null) return "[Lỗi trích xuất DOCX: Không tìm thấy document.xml]";

                    using (var entryStream = entry.Open())
                    {
                        var doc = System.Xml.Linq.XDocument.Load(entryStream);
                        System.Xml.Linq.XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                        
                        var paragraphs = doc.Descendants(w + "p");
                        var sb = new StringBuilder();
                        foreach (var p in paragraphs)
                        {
                            var texts = p.Descendants(w + "t").Select(t => t.Value);
                            sb.AppendLine(string.Concat(texts));
                        }
                        return sb.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                return $"[Lỗi trích xuất DOCX: {ex.Message}]";
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteDocument(int documentId)
        {
            var document = await _documentService.GetDocumentByIdAsync(documentId);
            if (document == null)
            {
                return Json(new { success = false, message = "Tài liệu không tồn tại." });
            }

            var auth = await CheckPermissionForChapterAsync(document.ChapterId);
            if (!auth.Success)
            {
                return Json(new { success = false, message = auth.Message });
            }

            try
            {
                int chapterId = document.ChapterId;
                string docTitle = document.Title;
                await _documentService.DeleteDocumentAsync(documentId);
                await _hubContext.Clients.All.SendAsync("ReceiveDocumentUpdate", chapterId);
                await _hubContext.Clients.All.SendAsync("ReceiveSystemNotification", "Xóa tài liệu", $"Tài liệu '{docTitle}' đã được xóa.", "warning");
                return Json(new { success = true, message = "Xóa tài liệu thành công!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa tài liệu {DocId}: {Message}", documentId, ex.Message);
                return Json(new { success = false, message = "Lỗi khi xóa tài liệu: " + ex.Message });
            }
        }

        // --- Indexing & Chunks previews ---

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IndexDocument(int documentId, int? modelId = null, int? strategyId = null, int? chunkSize = null, int? chunkOverlap = null)
        {
            try
            {
                var doc = await _documentService.GetDocumentByIdAsync(documentId);
                if (doc == null) return Json(new { success = false, message = "Tài liệu không tồn tại." });

                var chapter = await _documentService.GetChapterByIdAsync(doc.ChapterId);
                var subject = chapter != null ? await _documentService.GetSubjectByIdAsync(chapter.SubjectId) : null;

                // Resolve defaults
                int resolvedModelId = modelId ?? subject?.DefaultModelId ?? 1;
                int resolvedStrategyId = strategyId ?? subject?.DefaultStrategyId ?? 2;
                int resolvedChunkSize = chunkSize ?? subject?.DefaultChunkSize ?? 500;
                int resolvedChunkOverlap = chunkOverlap ?? subject?.DefaultChunkOverlap ?? 100;

                // Ensure selected model exists in db
                var allModels = await _documentService.GetAllEmbeddingModelsAsync();
                if (!allModels.Any(m => m.ModelId == resolvedModelId))
                {
                    resolvedModelId = allModels.FirstOrDefault()?.ModelId ?? 1;
                }

                // Ensure selected strategy exists in db
                var allStrategies = await _documentService.GetAllChunkingStrategiesAsync();
                if (!allStrategies.Any(s => s.StrategyId == resolvedStrategyId))
                {
                    resolvedStrategyId = allStrategies.FirstOrDefault()?.StrategyId ?? 1;
                }

                var index = await _documentService.IndexDocumentAsync(documentId, resolvedModelId, resolvedStrategyId, resolvedChunkSize, resolvedChunkOverlap);
                if (doc != null)
                {
                    await _hubContext.Clients.All.SendAsync("ReceiveDocumentUpdate", doc.ChapterId);
                    await _hubContext.Clients.All.SendAsync("ReceiveSystemNotification", "Chỉ mục tài liệu", $"Tài liệu '{doc.Title}' đã được lập chỉ mục (index) thành công!", "success");
                }
                return Json(new { success = true, message = "Lập chỉ mục RAG thành công!", indexId = index.IndexId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IndexDocument failed. DocumentId={DocumentId}, ModelId={ModelId}, StrategyId={StrategyId}. Details: {Details}",
                    documentId, modelId, strategyId, BuildExceptionDetails(ex));
                var docObj = await _documentService.GetDocumentByIdAsync(documentId);
                string titleStr = docObj?.Title ?? "Tài liệu";
                await _hubContext.Clients.All.SendAsync("ReceiveSystemNotification", "Lỗi chỉ mục", $"Tài liệu '{titleStr}' lập chỉ mục thất bại: {ex.Message}", "error");
                return Json(new { success = false, message = $"Lỗi khi phân nhỏ: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetIndexes(int documentId)
        {
            var indexes = await _documentService.GetIndexesByDocumentIdAsync(documentId);
            return Json(indexes.Select(idx => new {
                idx.IndexId,
                ModelName = idx.Model?.ModelName ?? "Embedding Model",
                StrategyName = idx.Strategy?.StrategyName ?? "Chunk Strategy",
                idx.ChunkSize,
                idx.ChunkOverlap,
                CreatedAt = idx.CreatedAt?.ToString("dd/MM/yyyy HH:mm")
            }));
        }

        [HttpGet]
        public async Task<IActionResult> GetChunks(int indexId)
        {
            var chunks = await _documentService.GetChunksByIndexIdAsync(indexId);
            return Json(chunks.Select(c => new {
                c.ChunkId,
                c.ChunkOrder,
                c.PageNumber,
                c.TokenCount,
                c.Content
            }));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReIndexAll(int subjectId)
        {
            try
            {
                var role = User.FindFirst(ClaimTypes.Role)?.Value;
                if (role != "Admin")
                {
                    var auth = await CheckPermissionForSubjectAsync(subjectId);
                    if (!auth.Success)
                    {
                        return Json(new { success = false, message = auth.Message });
                    }
                }

                var docs = (await _documentService.GetIndexedDocumentsAsync(subjectId)).ToList();
                if (!docs.Any())
                {
                    return Json(new { success = false, message = "Không có tài liệu indexed để re-index." });
                }

                var model = (await _documentService.GetAllEmbeddingModelsAsync()).FirstOrDefault();
                var strategy = (await _documentService.GetAllChunkingStrategiesAsync()).FirstOrDefault();
                if (model == null || strategy == null)
                {
                    return Json(new { success = false, message = "Thiếu cấu hình embedding model hoặc chunking strategy." });
                }

                int processed = 0;
                foreach (var doc in docs)
                {
                    await _documentService.IndexDocumentAsync(
                        doc.DocumentId,
                        model.ModelId,
                        strategy.StrategyId,
                        chunkSize: 500,
                        chunkOverlap: 100);
                    processed++;
                }

                return Json(new { success = true, message = $"Đã re-index {processed} tài liệu." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReIndexAll failed for subjectId={SubjectId}. Details: {Details}",
                    subjectId, BuildExceptionDetails(ex));
                return Json(new { success = false, message = $"Lỗi re-index: {ex.Message}" });
            }
        }

        // --- Helper Methods ---
        private string FormatBytes(long bytes)
        {
            string[] suf = { "B", "KB", "MB", "GB" };
            if (bytes == 0) return "0 B";
            long bytesAbs = Math.Abs(bytes);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytesAbs, 1024)));
            double num = Math.Round(bytesAbs / Math.Pow(1024, place), 1);
            return (Math.Sign(bytes) * num).ToString() + " " + suf[place];
        }

        private string GenerateCurriculumSimulationText(string title)
        {
            // Generates beautiful educational paragraphs matching PRN222 curriculum
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Học liệu RAG môn PRN222 - Chuyên đề: {title}");
            sb.AppendLine("1. Tổng quan lý thuyết");
            sb.AppendLine("Môn học PRN222 (Lập trình ứng dụng Web bằng ASP.NET Core Web MVC) tập trung vào kiến trúc phân lớp, cụ thể là 3-Layers Architecture.");
            sb.AppendLine("Kiến trúc này phân tách rõ ràng trách nhiệm giữa ba lớp cốt lõi:");
            sb.AppendLine("- Presentation Layer (Lớp giao diện): Chứa Controllers, Views và các thành phần giao tiếp trực tiếp với Client.");
            sb.AppendLine("- Business Logic Layer (BLL - Lớp nghiệp vụ): Chứa các lớp dịch vụ (Services), xử lý logic tính toán, xác thực dữ liệu và điều phối các thao tác.");
            sb.AppendLine("- Data Access Layer (DAL - Lớp dữ liệu): Chứa DbContext, thực thể (Entities), các lớp Repository và UnitOfWork dùng để truy vấn cơ sở dữ liệu SQL Server thông qua Entity Framework Core.");
            sb.AppendLine("\n2. Thực hành Repository và Singleton Pattern");
            sb.AppendLine("Repository Pattern hoạt động như một lớp trung gian giữa BLL và DAL, đóng gói các câu lệnh truy vấn EF Core. Lớp GenericRepository<T> triển khai interface IGenericRepository<T> giúp loại bỏ việc viết lặp lại các câu lệnh CRUD.");
            sb.AppendLine("Singleton Pattern được sử dụng để giới hạn sự khởi tạo của một lớp ở một đối tượng duy nhất trong suốt vòng đời ứng dụng. Ví dụ điển hình là các bộ máy lập chỉ mục (Embedding Engines) hoặc các dịch vụ cấu hình dùng chung toàn hệ thống.");
            sb.AppendLine("\n3. RAG và Kỹ thuật Chunking");
            sb.AppendLine("RAG (Retrieval-Augmented Generation) tăng cường khả năng của mô hình ngôn ngữ lớn (LLM) bằng cách tích hợp dữ liệu bên ngoài.");
            sb.AppendLine("Quy trình gồm 3 bước: Phân nhỏ văn bản (Chunking) theo kích thước (Chunk Size) và độ chồng lấp (Overlap); Biến đổi các đoạn text thành các vector số học (Embeddings); Lưu trữ các vector này vào Vector Databases (Qdrant, ChromaDB). Khi người dùng hỏi, hệ thống sẽ thực hiện so khớp Cosine Similarity để lấy ra các đoạn ngữ cảnh phù hợp nhất.");
            sb.AppendLine("\n4. Đánh giá kiểm thử và chỉ số RAGAS");
            sb.AppendLine("Hệ thống đo lường hiệu năng của RAG bằng 4 chỉ số chính của RAGAS:");
            sb.AppendLine("- Faithfulness (Mức độ trung thực): Đảm bảo câu trả lời của trợ lý hoàn toàn trích xuất và không xuyên tạc từ ngữ cảnh tài liệu học tập.");
            sb.AppendLine("- Answer Relevance (Mức độ phù hợp câu trả lời): Đảm bảo câu trả lời giải quyết trực tiếp và chính xác câu hỏi của người dùng.");
            sb.AppendLine("- Context Precision (Độ chính xác ngữ cảnh): Đo lường xem các đoạn tài liệu được trích xuất có thực sự liên quan mật thiết đến chủ đề câu hỏi hay không.");
            sb.AppendLine("- Context Recall (Độ phủ ngữ cảnh): Đo lường xem các ngữ cảnh trích xuất đã bao trùm đủ dữ liệu để trả lời đúng theo Ground Truth (đáp án chuẩn) hay chưa.");
            sb.AppendLine("\nKết thúc tài liệu học tập mẫu.");
            return sb.ToString();
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
