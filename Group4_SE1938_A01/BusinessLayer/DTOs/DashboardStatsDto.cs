using System;
using System.Collections.Generic;

namespace BusinessLayer.DTOs
{
    public class UserTokenStatsDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int TotalTokensIn { get; set; }
        public int TotalTokensOut { get; set; }
        public int TotalTokens => TotalTokensIn + TotalTokensOut;
        public int MessageCount { get; set; }
    }

    public class ModelComparisonDto
    {
        public string ModelName { get; set; } = string.Empty;
        public double AvgPrecision { get; set; }
        public double AvgRecall { get; set; }
        public double AvgMRR { get; set; }
        public double AvgLatency { get; set; }
        public int TestCount { get; set; }
    }

    public class TopDocumentDto
    {
        public string Title { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public int CitationCount { get; set; }
    }

    public class TokenOverTimeDto
    {
        public string DateStr { get; set; } = string.Empty;
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
    }

    public class HourlyActivityDto
    {
        public int Hour { get; set; }
        public int Count { get; set; }
    }

    public class WordFrequencyDto
    {
        public string Word { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class StrategyComparisonDto
    {
        public string StrategyName { get; set; } = string.Empty;
        public double AvgPrecision { get; set; }
        public double AvgRecall { get; set; }
        public double AvgMRR { get; set; }
        public double AvgLatency { get; set; }
        public int TestCount { get; set; }
    }

    public class SubjectTokenStatsDto
    {
        public string SubjectName { get; set; } = string.Empty;
        public string SubjectCode { get; set; } = string.Empty;
        public int TotalMessages { get; set; }
        public int TotalTokens { get; set; }
    }

    public class SubjectDocStatsDto
    {
        public string SubjectName { get; set; } = string.Empty;
        public string SubjectCode { get; set; } = string.Empty;
        public int DocumentCount { get; set; }
        public int IndexedCount { get; set; }
        public int ChunkCount { get; set; }
    }

    public class DashboardStatsDto
    {
        public List<UserTokenStatsDto> TokenStats { get; set; } = new();
        public List<ModelComparisonDto> ModelComparison { get; set; } = new();
        public List<TopDocumentDto> TopDocs { get; set; } = new();
        public List<TokenOverTimeDto> TokenOverTime { get; set; } = new();
        public List<HourlyActivityDto> HourlyActivity { get; set; } = new();
        public List<WordFrequencyDto> WordFrequencies { get; set; } = new();
        public List<StrategyComparisonDto> StrategyComparison { get; set; } = new();
        public List<SubjectTokenStatsDto> SubjectTokenStats { get; set; } = new();
        public List<SubjectDocStatsDto> SubjectDocStats { get; set; } = new();
    }
}
