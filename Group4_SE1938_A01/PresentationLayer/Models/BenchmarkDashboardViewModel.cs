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

    public class UserTokenStatsViewModel
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int TotalTokensIn { get; set; }
        public int TotalTokensOut { get; set; }
        public int TotalTokens => TotalTokensIn + TotalTokensOut;
        public int MessageCount { get; set; }
    }

    public class ModelComparisonViewModel
    {
        public string ModelName { get; set; } = string.Empty;
        public double AvgPrecision { get; set; }
        public double AvgRecall { get; set; }
        public double AvgMRR { get; set; }
        public double AvgLatency { get; set; }
        public int TestCount { get; set; }
    }

    public class TopDocumentViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public int CitationCount { get; set; }
    }

    public class WordFrequencyViewModel
    {
        public string Word { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class TokenOverTimeViewModel
    {
        public string DateStr { get; set; } = string.Empty;
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
    }

    public class HourlyActivityViewModel
    {
        public int Hour { get; set; }
        public int Count { get; set; }
    }

    public class StrategyComparisonViewModel
    {
        public string StrategyName { get; set; } = string.Empty;
        public double AvgPrecision { get; set; }
        public double AvgRecall { get; set; }
        public double AvgMRR { get; set; }
        public double AvgLatency { get; set; }
        public int TestCount { get; set; }
    }

    public class SubjectTokenStatsViewModel
    {
        public string SubjectName { get; set; } = string.Empty;
        public string SubjectCode { get; set; } = string.Empty;
        public int TotalMessages { get; set; }
        public int TotalTokens { get; set; }
        public double AvgTokensPerMessage => TotalMessages > 0 ? Math.Round((double)TotalTokens / TotalMessages, 0) : 0;
    }

    public class SubjectDocStatsViewModel
    {
        public string SubjectName { get; set; } = string.Empty;
        public string SubjectCode { get; set; } = string.Empty;
        public int DocumentCount { get; set; }
        public int IndexedCount { get; set; }
        public int ChunkCount { get; set; }
    }
}
