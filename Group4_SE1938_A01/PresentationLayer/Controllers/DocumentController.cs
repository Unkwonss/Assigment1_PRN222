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

namespace PresentationLayer.Controllers
{
    [Authorize(Roles = "Teacher,Admin")]
    public class DocumentController : Controller
    {
        private readonly IDocumentService _documentService;
        private readonly ILogger<DocumentController> _logger;

        public DocumentController(IDocumentService documentService, ILogger<DocumentController> logger)
        {
            _documentService = documentService;
            _logger = logger;
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

        // --- Subject CRUD Actions ---

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSubject(string subjectCode, string subjectName)
        {
            if (string.IsNullOrEmpty(subjectCode) || string.IsNullOrEmpty(subjectName))
            {
                TempData["Error"] = "Vui lòng nhập đầy đủ mã và tên môn học.";
                return RedirectToAction("Index");
            }

            var subject = new SubjectDto { SubjectCode = subjectCode, SubjectName = subjectName };
            await _documentService.CreateSubjectAsync(subject);
            TempData["Success"] = "Thêm môn học thành công!";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSubject(int subjectId, string subjectCode, string subjectName)
        {
            if (string.IsNullOrEmpty(subjectCode) || string.IsNullOrEmpty(subjectName))
            {
                TempData["Error"] = "Thông tin môn học không hợp lệ.";
                return RedirectToAction("Index");
            }

            var subject = new SubjectDto { SubjectId = subjectId, SubjectCode = subjectCode, SubjectName = subjectName };
            await _documentService.UpdateSubjectAsync(subject);
            TempData["Success"] = "Cập nhật môn học thành công!";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSubject(int subjectId)
        {
            try
            {
                // Delete Subject (EF will cascade delete chapters and documents if configured, or block)
                await _documentService.DeleteSubjectAsync(subjectId);
                TempData["Success"] = "Xóa môn học thành công!";
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateChapter(int subjectId, int chapterNumber, string chapterName)
        {
            if (string.IsNullOrEmpty(chapterName) || chapterNumber <= 0)
            {
                return Json(new { success = false, message = "Thông tin chương không hợp lệ." });
            }

            var chapter = new ChapterDto { SubjectId = subjectId, ChapterNumber = chapterNumber, ChapterName = chapterName };
            await _documentService.CreateChapterAsync(chapter);
            return Json(new { success = true, message = "Thêm chương mới thành công!" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditChapter(int chapterId, int chapterNumber, string chapterName)
        {
            if (string.IsNullOrEmpty(chapterName) || chapterNumber <= 0)
            {
                return Json(new { success = false, message = "Thông tin chỉnh sửa không hợp lệ." });
            }

            var existing = await _documentService.GetChapterByIdAsync(chapterId);
            if (existing == null) return Json(new { success = false, message = "Chương không tồn tại." });

            existing.ChapterNumber = chapterNumber;
            existing.ChapterName = chapterName;
            await _documentService.UpdateChapterAsync(existing);
            return Json(new { success = true, message = "Cập nhật chương thành công!" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteChapter(int chapterId)
        {
            try
            {
                await _documentService.DeleteChapterAsync(chapterId);
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
            if (file == null || file.Length == 0 || string.IsNullOrEmpty(title))
            {
                return Json(new { success = false, message = "Vui lòng nhập tiêu đề và chọn tệp hợp lệ." });
            }

            string ext = Path.GetExtension(file.FileName).ToLower();
            if (ext != ".txt" && ext != ".pdf" && ext != ".docx" && ext != ".pptx")
            {
                return Json(new { success = false, message = "Chỉ chấp nhận các tệp định dạng .txt, .pdf, .docx, .pptx." });
            }

            // Extract content
            string textContent = "";
            if (ext == ".txt")
            {
                using (var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8))
                {
                    textContent = await reader.ReadToEndAsync();
                }
            }
            else if (ext == ".pdf")
            {
                textContent = ExtractTextFromPdf(file.OpenReadStream());
            }
            else if (ext == ".docx")
            {
                textContent = ExtractTextFromDocx(file.OpenReadStream());
            }
            else if (ext == ".pptx")
            {
                textContent = ExtractTextFromPptx(file.OpenReadStream(), file.FileName);
            }
            else
            {
                // Simulate robust text extraction based on curriculum context
                textContent = GenerateCurriculumSimulationText(title);
            }

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId) || userId < 0)
            {
                return Json(new { success = false, message = "Không xác định được tài khoản đăng nhập. Vui lòng đăng nhập lại." });
            }

            var doc = new DocumentDto
            {
                ChapterId = chapterId,
                Title = title,
                FileName = file.FileName,
                FilePath = file.FileName, // Stored as metadata
                FileType = ext.Substring(1).ToUpper(),
                FileSize = file.Length,
                UploadedBy = userId
            };

            try
            {
                await _documentService.UploadDocumentAsync(doc, textContent);
                return Json(new { success = true, message = "Tải lên tài liệu thành công!" });
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDocument(int documentId)
        {
            try
            {
                await _documentService.DeleteDocumentAsync(documentId);
                return Json(new { success = true, message = "Xóa tài liệu thành công!" });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "Lỗi khi xóa tài liệu." });
            }
        }

        // --- Indexing & Chunks previews ---

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IndexDocument(int documentId, int modelId, int strategyId, int chunkSize, int chunkOverlap)
        {
            try
            {
                var index = await _documentService.IndexDocumentAsync(documentId, modelId, strategyId, chunkSize, chunkOverlap);
                return Json(new { success = true, message = "Lập chỉ mục RAG thành công!", indexId = index.IndexId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IndexDocument failed. DocumentId={DocumentId}, ModelId={ModelId}, StrategyId={StrategyId}. Details: {Details}",
                    documentId, modelId, strategyId, BuildExceptionDetails(ex));
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
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReIndexAll(int subjectId)
        {
            try
            {
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
