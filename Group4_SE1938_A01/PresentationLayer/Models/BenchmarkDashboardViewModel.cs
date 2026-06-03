using System;

namespace PresentationLayer.Models
{
    public class BenchmarkResultViewModel
    {
        public string ModelName { get; set; } = string.Empty;
        public string ChunkStrategy { get; set; } = string.Empty;
        public double Precision3 { get; set; }
        public double Recall3 { get; set; }
        public double MRR { get; set; }
        public double AvgLatencyMs { get; set; }
        public DateTime RunAt { get; set; }
    }
}
