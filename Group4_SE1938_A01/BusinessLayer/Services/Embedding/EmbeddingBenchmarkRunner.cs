using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BusinessLayer.Interfaces;

namespace BusinessLayer.Services.Embedding
{
    public class EmbeddingBenchmarkRunner
    {
        private readonly List<IEmbeddingProvider> _providers;

        public EmbeddingBenchmarkRunner(IEnumerable<IEmbeddingProvider> providers)
        {
            _providers = providers.ToList();
        }

        public async Task<List<BenchmarkResultData>> RunBenchmarkAsync(List<BenchmarkInput> inputs)
        {
            var results = new List<BenchmarkResultData>();

            foreach (var provider in _providers)
            {
                Console.WriteLine($"Starting benchmark for {provider.ModelName} ({provider.ProviderName})...");
                
                var result = new BenchmarkResultData
                {
                    ModelName = provider.ModelName,
                    ProviderName = provider.ProviderName,
                    TotalQueriesProcessed = inputs.Count
                };

                double totalPrecisionAt3 = 0;
                double totalRecallAt3 = 0;
                double totalMrr = 0;
                long totalLatencyMs = 0;

                foreach (var input in inputs)
                {
                    var sw = Stopwatch.StartNew();

                    // 1. Get embedding for the Question
                    var queryEmbedding = await provider.GetEmbeddingAsync(input.Question);

                    // 2. Get embeddings for all retrieved chunks
                    var chunkEmbeddings = new List<(DocumentChunkData Chunk, float[] Vector)>();
                    
                    // We can optimize by batching if the provider supports it well
                    var chunkTexts = input.RetrievedChunks.Select(c => c.Content).ToList();
                    var vectors = await provider.GetEmbeddingsAsync(chunkTexts);
                    
                    for (int i = 0; i < input.RetrievedChunks.Count; i++)
                    {
                        chunkEmbeddings.Add((input.RetrievedChunks[i], vectors[i]));
                    }

                    // 3. Compute cosine similarity and rank
                    var rankedChunks = chunkEmbeddings
                        .Select(ce => new
                        {
                            Chunk = ce.Chunk,
                            Score = ComputeCosineSimilarity(queryEmbedding, ce.Vector)
                        })
                        .OrderByDescending(x => x.Score)
                        .ToList();

                    sw.Stop();
                    totalLatencyMs += sw.ElapsedMilliseconds;

                    // 4. Calculate metrics for this query
                    // Assuming we know which chunks are relevant (IsRelevantToGroundTruth)
                    int totalRelevantInSet = input.RetrievedChunks.Count(c => c.IsRelevantToGroundTruth);
                    
                    if (totalRelevantInSet == 0)
                    {
                        // If there are no relevant chunks in the set, we skip metric calculation to avoid division by zero
                        continue; 
                    }

                    // Top 3 calculations
                    var top3 = rankedChunks.Take(3).ToList();
                    int relevantInTop3 = top3.Count(x => x.Chunk.IsRelevantToGroundTruth);

                    double precisionAt3 = (double)relevantInTop3 / 3.0;
                    double recallAt3 = (double)relevantInTop3 / totalRelevantInSet;

                    // MRR calculation (find rank of FIRST relevant document)
                    double reciprocalRank = 0;
                    for (int i = 0; i < rankedChunks.Count; i++)
                    {
                        if (rankedChunks[i].Chunk.IsRelevantToGroundTruth)
                        {
                            reciprocalRank = 1.0 / (i + 1);
                            break;
                        }
                    }

                    totalPrecisionAt3 += precisionAt3;
                    totalRecallAt3 += recallAt3;
                    totalMrr += reciprocalRank;
                }

                // Average metrics across all queries
                if (inputs.Count > 0)
                {
                    result.PrecisionAt3 = totalPrecisionAt3 / inputs.Count;
                    result.RecallAt3 = totalRecallAt3 / inputs.Count;
                    result.MeanReciprocalRank = totalMrr / inputs.Count;
                    result.AverageLatencyMs = (double)totalLatencyMs / inputs.Count;
                }

                results.Add(result);
                
                Console.WriteLine($"Finished {provider.ModelName}. Avg Latency: {result.AverageLatencyMs}ms. MRR: {result.MeanReciprocalRank}");
            }

            // Save to JSON
            await SaveResultsToFileAsync(results);

            return results;
        }

        private double ComputeCosineSimilarity(float[] vecA, float[] vecB)
        {
            if (vecA == null || vecB == null || vecA.Length != vecB.Length) return 0;
            
            double dotProduct = 0;
            double normA = 0;
            double normB = 0;
            
            for (int i = 0; i < vecA.Length; i++)
            {
                dotProduct += vecA[i] * vecB[i];
                normA += vecA[i] * vecA[i];
                normB += vecB[i] * vecB[i];
            }
            
            if (normA == 0 || normB == 0) return 0;
            
            return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }

        private async Task SaveResultsToFileAsync(List<BenchmarkResultData> results)
        {
            try
            {
                string directory = Path.Combine(Directory.GetCurrentDirectory(), "results");
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string fileName = $"benchmark_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                string filePath = Path.Combine(directory, fileName);

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(results, options);

                await File.WriteAllTextAsync(filePath, json);
                Console.WriteLine($"Saved benchmark results to {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving results: {ex.Message}");
            }
        }
    }
}
