using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Domain.Models;
using DataAccessLayer.Repository;
using BusinessLayer.Interfaces;
using BusinessLayer.DTOs;

namespace BusinessLayer.Services
{
    public class BenchmarkService : IBenchmarkService
    {
        private readonly IGenericRepository<Experiment> _experimentRepo;
        private readonly IGenericRepository<TestSet> _testSetRepo;
        private readonly IGenericRepository<BenchmarkResult> _resultRepo;
        private readonly IGenericRepository<Aimodel> _aiModelRepo;
        private readonly IGenericRepository<Document> _documentRepo;
        private readonly IGenericRepository<DocumentIndex> _indexRepo;
        private readonly IGenericRepository<DocumentChunk> _chunkRepo;
        private readonly SimulatedAIEngine _aiEngine;

        public BenchmarkService(
            IGenericRepository<Experiment> experimentRepo,
            IGenericRepository<TestSet> testSetRepo,
            IGenericRepository<BenchmarkResult> resultRepo,
            IGenericRepository<Aimodel> aiModelRepo,
            IGenericRepository<Document> documentRepo,
            IGenericRepository<DocumentIndex> indexRepo,
            IGenericRepository<DocumentChunk> chunkRepo,
            SimulatedAIEngine aiEngine)
        {
            _experimentRepo = experimentRepo;
            _testSetRepo = testSetRepo;
            _resultRepo = resultRepo;
            _aiModelRepo = aiModelRepo;
            _documentRepo = documentRepo;
            _indexRepo = indexRepo;
            _chunkRepo = chunkRepo;
            _aiEngine = aiEngine;
        }

        #region Mappers
        private AimodelDto? MapAimodelToDto(Aimodel? model)
        {
            if (model == null) return null;
            return new AimodelDto
            {
                ModelId = model.ModelId,
                ModelName = model.ModelName,
                ModelType = model.ModelType
            };
        }

        private Aimodel? MapAimodelToEntity(AimodelDto? dto)
        {
            if (dto == null) return null;
            return new Aimodel
            {
                ModelId = dto.ModelId,
                ModelName = dto.ModelName,
                ModelType = dto.ModelType
            };
        }

        private EmbeddingModelDto? MapEmbeddingModelToDto(EmbeddingModel? model)
        {
            if (model == null) return null;
            return new EmbeddingModelDto
            {
                ModelId = model.ModelId,
                ModelName = model.ModelName,
                Provider = model.Provider
            };
        }

        private EmbeddingModel? MapEmbeddingModelToEntity(EmbeddingModelDto? dto)
        {
            if (dto == null) return null;
            return new EmbeddingModel
            {
                ModelId = dto.ModelId,
                ModelName = dto.ModelName,
                Provider = dto.Provider
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

        private ExperimentDto? MapExperimentToDto(Experiment? exp)
        {
            if (exp == null) return null;
            return new ExperimentDto
            {
                ExperimentId = exp.ExperimentId,
                ExperimentName = exp.ExperimentName,
                ExperimentDescription = exp.ExperimentDescription,
                AimodelId = exp.AimodelId,
                EmbeddingModelId = exp.EmbeddingModelId,
                StrategyId = exp.StrategyId,
                ChunkSize = exp.ChunkSize,
                ChunkOverlap = exp.ChunkOverlap,
                Aimodel = MapAimodelToDto(exp.Aimodel),
                EmbeddingModel = MapEmbeddingModelToDto(exp.EmbeddingModel),
                Strategy = MapStrategyToDto(exp.Strategy)
            };
        }

        private Experiment? MapExperimentToEntity(ExperimentDto? dto)
        {
            if (dto == null) return null;
            return new Experiment
            {
                ExperimentId = dto.ExperimentId,
                ExperimentName = dto.ExperimentName,
                ExperimentDescription = dto.ExperimentDescription,
                AimodelId = dto.AimodelId,
                EmbeddingModelId = dto.EmbeddingModelId,
                StrategyId = dto.StrategyId,
                ChunkSize = dto.ChunkSize,
                ChunkOverlap = dto.ChunkOverlap
            };
        }

        private TestSetDto? MapTestSetToDto(TestSet? testSet)
        {
            if (testSet == null) return null;
            return new TestSetDto
            {
                QuestionId = testSet.QuestionId,
                SubjectId = testSet.SubjectId,
                Question = testSet.Question,
                GroundTruth = testSet.GroundTruth,
                CreatedAt = testSet.CreatedAt
            };
        }

        private TestSet? MapTestSetToEntity(TestSetDto? dto)
        {
            if (dto == null) return null;
            return new TestSet
            {
                QuestionId = dto.QuestionId,
                SubjectId = dto.SubjectId,
                Question = dto.Question,
                GroundTruth = dto.GroundTruth,
                CreatedAt = dto.CreatedAt
            };
        }

        private BenchmarkResultDto? MapResultToDto(BenchmarkResult? res)
        {
            if (res == null) return null;
            return new BenchmarkResultDto
            {
                ResultId = res.ResultId,
                QuestionId = res.QuestionId,
                ExperimentId = res.ExperimentId,
                GeneratedResponse = res.GeneratedResponse,
                LatencyMilliseconds = res.LatencyMilliseconds,
                TokensIn = res.TokensIn,
                TokensOut = res.TokensOut,
                ErrorMessage = res.ErrorMessage,
                FaithfulnessScore = res.FaithfulnessScore,
                AnswerRelevanceScore = res.AnswerRelevanceScore,
                ContextPrecisionScore = res.ContextPrecisionScore,
                ContextRecallScore = res.ContextRecallScore,
                TestedAt = res.TestedAt,
                Experiment = MapExperimentToDto(res.Experiment),
                Question = MapTestSetToDto(res.Question)
            };
        }
        #endregion

        #region Experiments
        public async Task<IEnumerable<ExperimentDto>> GetAllExperimentsAsync()
        {
            var experiments = await _experimentRepo.GetAllAsync(includeProperties: "Aimodel,EmbeddingModel,Strategy");
            return experiments.Select(e => MapExperimentToDto(e)!).ToList();
        }

        public async Task<ExperimentDto?> GetExperimentByIdAsync(int id)
        {
            var experiment = await _experimentRepo.GetFirstOrDefaultAsync(
                filter: e => e.ExperimentId == id,
                includeProperties: "Aimodel,EmbeddingModel,Strategy"
            );
            return MapExperimentToDto(experiment);
        }

        public async Task<ExperimentDto> CreateExperimentAsync(ExperimentDto experimentDto)
        {
            var experiment = MapExperimentToEntity(experimentDto)!;
            await _experimentRepo.AddAsync(experiment);
            await _experimentRepo.SaveAsync();
            return MapExperimentToDto(experiment)!;
        }

        public async Task UpdateExperimentAsync(ExperimentDto experimentDto)
        {
            var existing = await _experimentRepo.GetByIdAsync(experimentDto.ExperimentId);
            if (existing != null)
            {
                existing.ExperimentName = experimentDto.ExperimentName;
                existing.ExperimentDescription = experimentDto.ExperimentDescription;
                existing.AimodelId = experimentDto.AimodelId;
                existing.EmbeddingModelId = experimentDto.EmbeddingModelId;
                existing.StrategyId = experimentDto.StrategyId;
                existing.ChunkSize = experimentDto.ChunkSize;
                existing.ChunkOverlap = experimentDto.ChunkOverlap;
                
                _experimentRepo.Update(existing);
                await _experimentRepo.SaveAsync();
            }
        }

        public async Task DeleteExperimentAsync(int id)
        {
            await _experimentRepo.DeleteByIdAsync(id);
            await _experimentRepo.SaveAsync();
        }
        #endregion

        #region Test Sets
        public async Task<IEnumerable<TestSetDto>> GetTestSetsBySubjectIdAsync(int subjectId)
        {
            var testSets = await _testSetRepo.GetAllAsync(
                filter: t => t.SubjectId == subjectId,
                orderBy: q => q.OrderByDescending(t => t.CreatedAt)
            );
            return testSets.Select(t => MapTestSetToDto(t)!).ToList();
        }

        public async Task<TestSetDto?> GetTestSetByIdAsync(int id)
        {
            var testSet = await _testSetRepo.GetByIdAsync(id);
            return MapTestSetToDto(testSet);
        }

        public async Task<TestSetDto> CreateTestSetAsync(TestSetDto testSetDto)
        {
            var testSet = MapTestSetToEntity(testSetDto)!;
            await _testSetRepo.AddAsync(testSet);
            await _testSetRepo.SaveAsync();
            return MapTestSetToDto(testSet)!;
        }

        public async Task UpdateTestSetAsync(TestSetDto testSetDto)
        {
            var existing = await _testSetRepo.GetByIdAsync(testSetDto.QuestionId);
            if (existing != null)
            {
                existing.Question = testSetDto.Question;
                existing.GroundTruth = testSetDto.GroundTruth;
                _testSetRepo.Update(existing);
                await _testSetRepo.SaveAsync();
            }
        }

        public async Task DeleteTestSetAsync(int id)
        {
            await _testSetRepo.DeleteByIdAsync(id);
            await _testSetRepo.SaveAsync();
        }
        #endregion

        #region AI Models
        public async Task<IEnumerable<AimodelDto>> GetAllAIModelsAsync()
        {
            var aiModels = await _aiModelRepo.GetAllAsync();
            return aiModels.Select(m => MapAimodelToDto(m)!).ToList();
        }
        #endregion

        #region Running Benchmarks
        public async Task<IEnumerable<BenchmarkResultDto>> RunBenchmarkAsync(int experimentId, int subjectId)
        {
            var experiment = await _experimentRepo.GetFirstOrDefaultAsync(e => e.ExperimentId == experimentId, "Aimodel");
            if (experiment == null) throw new ArgumentException("Thử nghiệm không tồn tại.");

            var testSets = await _testSetRepo.GetAllAsync(t => t.SubjectId == subjectId);
            if (!testSets.Any()) return new List<BenchmarkResultDto>();

            // Find all documents for this subject
            var docs = await _documentRepo.GetAllAsync(d => d.Chapter.SubjectId == subjectId && d.Status == "Indexed");
            var docIds = docs.Select(d => d.DocumentId).ToList();

            List<DocumentChunk> allChunks = new List<DocumentChunk>();
            if (docIds.Any() && experiment.EmbeddingModelId.HasValue && experiment.StrategyId.HasValue)
            {
                var indexes = await _indexRepo.GetAllAsync(idx => 
                    docIds.Contains(idx.DocumentId) && 
                    idx.ModelId == experiment.EmbeddingModelId.Value && 
                    idx.StrategyId == experiment.StrategyId.Value && 
                    idx.ChunkSize == (experiment.ChunkSize ?? 500) && 
                    idx.ChunkOverlap == (experiment.ChunkOverlap ?? 100)
                );

                var indexIds = indexes.Select(idx => idx.IndexId).ToList();
                if (indexIds.Any())
                {
                    var dbChunks = await _chunkRepo.GetAllAsync(c => indexIds.Contains(c.IndexId));
                    allChunks.AddRange(dbChunks);
                }
            }

            var resultsList = new List<BenchmarkResultDto>();
            Random rand = new Random();

            foreach (var q in testSets)
            {
                var stopwatch = Stopwatch.StartNew();

                string response = "";
                List<string> retrievedTexts = new List<string>();

                // Clear previous results for this exact question and experiment to avoid duplication
                var existingResults = await _resultRepo.GetAllAsync(r => r.QuestionId == q.QuestionId && r.ExperimentId == experimentId);
                foreach (var oldRes in existingResults)
                {
                    _resultRepo.Delete(oldRes);
                }
                await _resultRepo.SaveAsync();

                if (experiment.Aimodel.ModelType == "Base-RAG")
                {
                    // RAG Retrieval simulation
                    if (allChunks.Any())
                    {
                        float[] qVec = _aiEngine.GenerateEmbedding(q.Question);
                        var ranked = allChunks.Select(c => new {
                            Chunk = c,
                            Score = _aiEngine.ComputeCosineSimilarity(qVec, _aiEngine.GenerateEmbedding(c.Content))
                        })
                        .Where(rc => rc.Score > 0.05)
                        .OrderByDescending(rc => rc.Score)
                        .Take(3)
                        .ToList();

                        if (ranked.Any())
                        {
                            retrievedTexts = ranked.Select(r => r.Chunk.Content).ToList();
                            var contexts = ranked.Select(r => (r.Chunk.ChunkId, r.Chunk.Content, r.Chunk.PageNumber)).ToList();
                            response = _aiEngine.GenerateRAGResponse(q.Question, contexts, "Benchmark");
                        }
                    }
                    
                    if (string.IsNullOrEmpty(response))
                    {
                        response = $"[Simulated RAG Assistant] Dựa trên kết quả truy xuất rỗng của thử nghiệm {experiment.ExperimentName}, câu trả lời không có trong tài liệu huấn luyện.";
                    }
                }
                else // Fine-tuned LLM
                {
                    // Direct generation without context retrieval
                    response = $"[Simulated Fine-Tuned LLM ({experiment.Aimodel.ModelName})] " +
                               $"Câu trả lời được sinh trực tiếp từ tham số học máy tinh chỉnh:\n" +
                               $"{q.GroundTruth}\n" +
                               $"(Không có văn bản truy xuất từ ngữ cảnh tài liệu).";
                }

                stopwatch.Stop();

                // Compute Ragas Scores
                var ragas = _aiEngine.CalculateRagasScores(q.Question, q.GroundTruth, response, retrievedTexts);

                // For fine-tuned, adjust context metrics to reflect lack of retrieval
                double finalPrecision = experiment.Aimodel.ModelType == "Fine-tuned" ? 0.0 : ragas.Precision;
                double finalRecall = experiment.Aimodel.ModelType == "Fine-tuned" ? 0.0 : ragas.Recall;

                // Simulated Latency
                int baseLatency = experiment.Aimodel.ModelType == "Base-RAG" ? 1200 : 700;
                int latency = baseLatency + rand.Next(100, 600) + (int)stopwatch.ElapsedMilliseconds;

                // Simulated tokens
                int tokensIn = 450 + rand.Next(50, 400);
                int tokensOut = 180 + rand.Next(20, 150);

                var res = new BenchmarkResult
                {
                    QuestionId = q.QuestionId,
                    ExperimentId = experimentId,
                    GeneratedResponse = response,
                    LatencyMilliseconds = latency,
                    TokensIn = tokensIn,
                    TokensOut = tokensOut,
                    FaithfulnessScore = ragas.Faithfulness,
                    AnswerRelevanceScore = ragas.Relevance,
                    ContextPrecisionScore = finalPrecision,
                    ContextRecallScore = finalRecall,
                    TestedAt = DateTime.UtcNow
                };

                await _resultRepo.AddAsync(res);
                
                // create dto and reload navigation
                var resDto = new BenchmarkResultDto
                {
                    ResultId = res.ResultId,
                    QuestionId = res.QuestionId,
                    ExperimentId = res.ExperimentId,
                    GeneratedResponse = res.GeneratedResponse,
                    LatencyMilliseconds = res.LatencyMilliseconds,
                    TokensIn = res.TokensIn,
                    TokensOut = res.TokensOut,
                    FaithfulnessScore = res.FaithfulnessScore,
                    AnswerRelevanceScore = res.AnswerRelevanceScore,
                    ContextPrecisionScore = res.ContextPrecisionScore,
                    ContextRecallScore = res.ContextRecallScore,
                    TestedAt = res.TestedAt,
                    Experiment = MapExperimentToDto(experiment),
                    Question = MapTestSetToDto(q)
                };
                resultsList.Add(resDto);
            }

            await _resultRepo.SaveAsync();
            return resultsList;
        }

        public async Task<IEnumerable<BenchmarkResultDto>> GetResultsByExperimentIdAsync(int experimentId)
        {
            var results = await _resultRepo.GetAllAsync(
                filter: r => r.ExperimentId == experimentId,
                includeProperties: "Question,Experiment,Experiment.Aimodel"
            );
            return results.Select(r => MapResultToDto(r)!).ToList();
        }

        public async Task<IEnumerable<BenchmarkResultDto>> GetAllResultsAsync()
        {
            var results = await _resultRepo.GetAllAsync(
                includeProperties: "Question,Experiment,Experiment.Aimodel"
            );
            return results.Select(r => MapResultToDto(r)!).ToList();
        }
        #endregion
    }
}
