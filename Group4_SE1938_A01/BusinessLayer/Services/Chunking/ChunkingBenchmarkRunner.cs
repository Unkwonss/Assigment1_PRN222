using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BusinessLayer.Interfaces;

namespace BusinessLayer.Services.Chunking
{
    public class ChunkingBenchmarkRunner
    {
        private readonly List<IChunkingStrategy> _strategies;
        private readonly IEmbeddingProvider _embeddingProvider;

        public ChunkingBenchmarkRunner(IEnumerable<IChunkingStrategy> strategies, IEmbeddingProvider embeddingProvider)
        {
            _strategies = strategies.ToList();
            _embeddingProvider = embeddingProvider;
        }

        public async Task<List<ChunkingBenchmarkResultData>> RunBenchmarkAsync(List<ChunkingBenchmarkInput> inputs)
        {
            var results = new List<ChunkingBenchmarkResultData>();

            foreach (var strategy in _strategies)
            {
                var result = new ChunkingBenchmarkResultData
                {
                    ModelName = _embeddingProvider.ModelName,
                    ChunkStrategy = strategy.StrategyName
                };

                double totalPrecisionAt3 = 0;
                double totalRecallAt3 = 0;
                double totalMrr = 0;
                long totalLatencyMs = 0;
                int totalChunksGenerated = 0;

                foreach (var input in inputs)
                {
                    var sw = Stopwatch.StartNew();

                    // 1. Chunk the document
                    var chunks = strategy.Chunk(input.DocumentText);
                    totalChunksGenerated += chunks.Count;

                    if (chunks.Count == 0) continue;

                    // 2. Embed the question and all chunks
                    var queryEmbedding = await _embeddingProvider.GetEmbeddingAsync(input.Question);
                    var chunkEmbeddings = await _embeddingProvider.GetEmbeddingsAsync(chunks);

                    // 3. Compute cosine similarity
                    var rankedChunks = new List<(string Chunk, double Score)>();
                    for (int i = 0; i < chunks.Count; i++)
                    {
                        double score = ComputeCosineSimilarity(queryEmbedding, chunkEmbeddings[i]);
                        rankedChunks.Add((chunks[i], score));
                    }

                    rankedChunks = rankedChunks.OrderByDescending(x => x.Score).ToList();
                    sw.Stop();
                    totalLatencyMs += sw.ElapsedMilliseconds;

                    // 4. Metrics calculation (Simulated by checking string overlap with Ground Truth)
                    // In a real scenario, we check if Ground Truth is logically in the chunk.
                    // Here we approximate relevance: if chunk contains >= 30% of Ground Truth keywords.
                    var gtWords = ExtractKeywords(input.GroundTruth);
                    
                    int totalRelevantInSet = 0;
                    var relevanceList = new List<bool>();
                    foreach(var rc in rankedChunks)
                    {
                        bool isRelevant = IsChunkRelevant(rc.Chunk, gtWords);
                        relevanceList.Add(isRelevant);
                        if (isRelevant) totalRelevantInSet++;
                    }

                    if (totalRelevantInSet == 0) continue; // skip division by zero

                    // Precision@3, Recall@3
                    int relevantInTop3 = relevanceList.Take(3).Count(r => r);
                    totalPrecisionAt3 += (double)relevantInTop3 / 3.0;
                    totalRecallAt3 += (double)relevantInTop3 / totalRelevantInSet;

                    // MRR
                    double reciprocalRank = 0;
                    for (int i = 0; i < relevanceList.Count; i++)
                    {
                        if (relevanceList[i])
                        {
                            reciprocalRank = 1.0 / (i + 1);
                            break;
                        }
                    }
                    totalMrr += reciprocalRank;
                }

                if (inputs.Count > 0)
                {
                    result.Precision3 = totalPrecisionAt3 / inputs.Count;
                    result.Recall3 = totalRecallAt3 / inputs.Count;
                    result.MRR = totalMrr / inputs.Count;
                    result.AvgLatencyMs = (double)totalLatencyMs / inputs.Count;
                    result.NumberOfChunksGenerated = totalChunksGenerated;
                }

                results.Add(result);
            }

            await SaveResultsToFileAsync(results);
            return results;
        }

        private double ComputeCosineSimilarity(float[] vecA, float[] vecB)
        {
            if (vecA == null || vecB == null || vecA.Length != vecB.Length) return 0;
            double dot = 0, normA = 0, normB = 0;
            for (int i = 0; i < vecA.Length; i++)
            {
                dot += vecA[i] * vecB[i];
                normA += vecA[i] * vecA[i];
                normB += vecB[i] * vecB[i];
            }
            if (normA == 0 || normB == 0) return 0;
            return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }

        private HashSet<string> ExtractKeywords(string text)
        {
            var words = text.ToLower().Split(new[] { ' ', '.', ',', '?', '!' }, StringSplitOptions.RemoveEmptyEntries);
            return new HashSet<string>(words);
        }

        private bool IsChunkRelevant(string chunk, HashSet<string> gtWords)
        {
            if (gtWords.Count == 0) return false;
            var chunkWords = ExtractKeywords(chunk);
            int overlap = 0;
            foreach (var w in gtWords)
            {
                if (chunkWords.Contains(w)) overlap++;
            }
            return (double)overlap / gtWords.Count > 0.3; // 30% overlap threshold
        }

        private async Task SaveResultsToFileAsync(List<ChunkingBenchmarkResultData> results)
        {
            try
            {
                string directory = Path.Combine(Directory.GetCurrentDirectory(), "results");
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                
                string fileName = $"chunking_benchmark_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                string filePath = Path.Combine(directory, fileName);
                
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(results, options);
                
                await File.WriteAllTextAsync(filePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving chunking results: {ex.Message}");
            }
        }
    }
}
