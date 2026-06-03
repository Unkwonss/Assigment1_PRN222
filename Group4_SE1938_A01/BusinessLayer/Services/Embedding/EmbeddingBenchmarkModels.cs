using System;
using System.Collections.Generic;

namespace BusinessLayer.Services.Embedding
{
    public class BenchmarkInput
    {
        public string Question { get; set; } = string.Empty;
        public string GroundTruth { get; set; } = string.Empty;
        
        // List of document chunks that need to be embedded and compared
        public List<DocumentChunkData> RetrievedChunks { get; set; } = new List<DocumentChunkData>();
    }

    public class DocumentChunkData
    {
        public int ChunkId { get; set; }
        public string Content { get; set; } = string.Empty;
        // In a real scenario, you'd know if this chunk is part of the ground truth.
        // For evaluation without knowing if chunk == ground truth, 
        // we might compare chunk text to ground truth text later.
        public bool IsRelevantToGroundTruth { get; set; } 
    }

    public class BenchmarkResultData
    {
        public string ModelName { get; set; } = string.Empty;
        public string ProviderName { get; set; } = string.Empty;
        
        // Metrics
        public double PrecisionAt3 { get; set; }
        public double RecallAt3 { get; set; }
        public double MeanReciprocalRank { get; set; }
        
        // Performance
        public double AverageLatencyMs { get; set; }
        public int TotalQueriesProcessed { get; set; }
        
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
