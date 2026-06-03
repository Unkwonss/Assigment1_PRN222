using System;
using System.Collections.Generic;

namespace Domain.Models;

public partial class BenchmarkResult
{
    public int ResultId { get; set; }

    public int QuestionId { get; set; }

    public int ExperimentId { get; set; }

    public string GeneratedResponse { get; set; } = null!;

    public int LatencyMilliseconds { get; set; }

    public int? TokensIn { get; set; }

    public int? TokensOut { get; set; }

    public string? ErrorMessage { get; set; }

    public double? FaithfulnessScore { get; set; }

    public double? AnswerRelevanceScore { get; set; }

    public double? ContextPrecisionScore { get; set; }

    public double? ContextRecallScore { get; set; }

    public DateTime? TestedAt { get; set; }

    public virtual Experiment Experiment { get; set; } = null!;

    public virtual TestSet Question { get; set; } = null!;
}
