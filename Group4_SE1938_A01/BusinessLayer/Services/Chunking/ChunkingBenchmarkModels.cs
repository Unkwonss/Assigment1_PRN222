using System;
using System.Collections.Generic;

namespace BusinessLayer.Services.Chunking
{
    public class ChunkingBenchmarkInput
    {
        public string DocumentText { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string GroundTruth { get; set; } = string.Empty;
    }

    public class ChunkingBenchmarkResultData
    {
        public string ModelName { get; set; } = "Default Embedding";
        public string ChunkStrategy { get; set; } = string.Empty;
        public int NumberOfChunksGenerated { get; set; }
        public double Precision3 { get; set; }
        public double Recall3 { get; set; }
        public double MRR { get; set; }
        public double AvgLatencyMs { get; set; }
        public DateTime RunAt { get; set; } = DateTime.UtcNow;
    }
}
