using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BusinessLayer.DTOs;
using BusinessLayer.Interfaces;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using PresentationLayer.Models;

namespace PresentationLayer.Controllers
{
    [Authorize(Roles = "Teacher,Admin")]
    public class BenchmarkController : Controller
    {
        private readonly IBenchmarkService _benchmarkService;
        private readonly IDocumentService _documentService;

        public BenchmarkController(IBenchmarkService benchmarkService, IDocumentService documentService)
        {
            _benchmarkService = benchmarkService;
            _documentService = documentService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var subjects = await _documentService.GetAllSubjectsAsync();
            var experiments = await _benchmarkService.GetAllExperimentsAsync();
            var aiModels = await _benchmarkService.GetAllAIModelsAsync();
            var embeddingModels = await _documentService.GetAllEmbeddingModelsAsync();
            var strategies = await _documentService.GetAllChunkingStrategiesAsync();

            ViewBag.Subjects = subjects;
            ViewBag.AIModels = aiModels;
            ViewBag.EmbeddingModels = embeddingModels;
            ViewBag.Strategies = strategies;

            return View(experiments);
        }

        // --- AJAX Experiment Configuration ---

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateExperiment(string experimentName, string experimentDescription, int aiModelId, int? embeddingModelId, int? strategyId, int? chunkSize, int? chunkOverlap)
        {
            if (string.IsNullOrEmpty(experimentName) || aiModelId <= 0)
            {
                TempData["Error"] = "Vui lòng nhập đầy đủ tên thử nghiệm và cấu hình mô hình AI.";
                return RedirectToAction("Index");
            }

            var exp = new ExperimentDto
            {
                ExperimentName = experimentName,
                ExperimentDescription = experimentDescription,
                AimodelId = aiModelId,
                EmbeddingModelId = embeddingModelId,
                StrategyId = strategyId,
                ChunkSize = chunkSize,
                ChunkOverlap = chunkOverlap
            };

            try
            {
                await _benchmarkService.CreateExperimentAsync(exp);
                TempData["Success"] = "Khởi tạo thử nghiệm nghiên cứu thành công!";
            }
            catch (Exception)
            {
                TempData["Error"] = "Tên thử nghiệm đã tồn tại hoặc cấu hình không hợp lệ.";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteExperiment(int experimentId)
        {
            try
            {
                await _benchmarkService.DeleteExperimentAsync(experimentId);
                TempData["Success"] = "Xóa thử nghiệm thành công!";
            }
            catch (Exception)
            {
                TempData["Error"] = "Không thể xóa thử nghiệm này.";
            }
            return RedirectToAction("Index");
        }

        // --- AJAX Test Sets (Questions & Ground Truth) ---

        [HttpGet]
        public async Task<IActionResult> GetTestSets(int subjectId)
        {
            var testSets = await _benchmarkService.GetTestSetsBySubjectIdAsync(subjectId);
            return Json(testSets.Select(t => new {
                t.QuestionId,
                t.Question,
                t.GroundTruth,
                CreatedAt = t.CreatedAt?.ToString("dd/MM/yyyy")
            }));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTestSet(int subjectId, string question, string groundTruth)
        {
            if (string.IsNullOrEmpty(question) || string.IsNullOrEmpty(groundTruth))
            {
                return Json(new { success = false, message = "Câu hỏi và đáp án chuẩn không được để trống." });
            }

            var ts = new TestSetDto
            {
                SubjectId = subjectId,
                Question = question,
                GroundTruth = groundTruth,
                CreatedAt = DateTime.UtcNow
            };

            await _benchmarkService.CreateTestSetAsync(ts);
            return Json(new { success = true, message = "Thêm câu hỏi mẫu thành công!" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTestSet(int questionId, string question, string groundTruth)
        {
            if (string.IsNullOrEmpty(question) || string.IsNullOrEmpty(groundTruth))
            {
                return Json(new { success = false, message = "Thông tin không hợp lệ." });
            }

            var existing = await _benchmarkService.GetTestSetByIdAsync(questionId);
            if (existing == null) return Json(new { success = false, message = "Câu hỏi không tồn tại." });

            existing.Question = question;
            existing.GroundTruth = groundTruth;
            await _benchmarkService.UpdateTestSetAsync(existing);
            return Json(new { success = true, message = "Cập nhật câu hỏi thành công!" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTestSet(int questionId)
        {
            try
            {
                await _benchmarkService.DeleteTestSetAsync(questionId);
                return Json(new { success = true, message = "Xóa câu hỏi kiểm thử thành công!" });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "Lỗi khi xóa câu hỏi." });
            }
        }

        // --- AJAX Run Benchmark & Retrieve Results ---

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunBenchmark(int experimentId, int subjectId)
        {
            try
            {
                var results = await _benchmarkService.RunBenchmarkAsync(experimentId, subjectId);
                if (!results.Any())
                {
                    return Json(new { success = false, message = "Môn học được chọn hiện tại chưa có bộ câu hỏi kiểm thử (Test Set) nào." });
                }
                return Json(new { success = true, message = $"Chạy thành công bộ đánh giá RBL ({results.Count()} câu hỏi)!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi thực thi thử nghiệm: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetBenchmarkResults(int experimentId)
        {
            var results = await _benchmarkService.GetResultsByExperimentIdAsync(experimentId);
            return Json(results.Select(r => new {
                r.ResultId,
                QuestionText = r.Question?.Question ?? "Câu hỏi",
                GroundTruthText = r.Question?.GroundTruth ?? "Đáp án chuẩn",
                r.GeneratedResponse,
                r.LatencyMilliseconds,
                r.TokensIn,
                r.TokensOut,
                Faithfulness = r.FaithfulnessScore ?? 0.0,
                Relevance = r.AnswerRelevanceScore ?? 0.0,
                Precision = r.ContextPrecisionScore ?? 0.0,
                Recall = r.ContextRecallScore ?? 0.0,
                TestedAt = r.TestedAt?.ToString("dd/MM/yyyy HH:mm")
            }));
        }

        [HttpGet]
        public async Task<IActionResult> GetAllBenchmarkResults()
        {
            var results = await _benchmarkService.GetAllResultsAsync();
            return Json(results.Select(r => new {
                r.ResultId,
                ExperimentName = r.Experiment?.ExperimentName ?? "Thử nghiệm",
                ModelType = r.Experiment?.Aimodel?.ModelType ?? "Base-RAG",
                QuestionText = r.Question?.Question ?? "Câu hỏi",
                r.LatencyMilliseconds,
                r.TokensIn,
                r.TokensOut,
                Faithfulness = r.FaithfulnessScore ?? 0.0,
                Relevance = r.AnswerRelevanceScore ?? 0.0,
                Precision = r.ContextPrecisionScore ?? 0.0,
                Recall = r.ContextRecallScore ?? 0.0
            }));
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            // Load benchmark results trực tiếp từ DB (không đọc file JSON nữa)
            var allResults = await _benchmarkService.GetAllResultsAsync();

            var viewModels = allResults.Select(r => new BenchmarkResultViewModel
            {
                ModelName        = r.Experiment?.Aimodel?.ModelName ?? "Unknown Model",
                ChunkStrategy    = r.Experiment?.Strategy?.StrategyName ?? "Default",
                Precision3       = r.ContextPrecisionScore ?? 0,
                Recall3          = r.ContextRecallScore ?? 0,
                MRR              = r.FaithfulnessScore ?? 0,
                AvgLatencyMs     = r.LatencyMilliseconds,
                RunAt            = r.TestedAt ?? DateTime.UtcNow
            }).ToList();

            return View(viewModels);
        }
    }
}
