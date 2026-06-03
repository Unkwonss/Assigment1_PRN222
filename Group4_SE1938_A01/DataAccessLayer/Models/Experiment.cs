using System;
using System.Collections.Generic;

namespace Domain.Models;

public partial class Experiment
{
    public int ExperimentId { get; set; }

    public string ExperimentName { get; set; } = null!;

    public string? ExperimentDescription { get; set; }

    public int AimodelId { get; set; }

    public int? EmbeddingModelId { get; set; }

    public int? StrategyId { get; set; }

    public int? ChunkSize { get; set; }

    public int? ChunkOverlap { get; set; }

    public virtual Aimodel Aimodel { get; set; } = null!;

    public virtual ICollection<BenchmarkResult> BenchmarkResults { get; set; } = new List<BenchmarkResult>();

    public virtual EmbeddingModel? EmbeddingModel { get; set; }

    public virtual ChunkingStrategy? Strategy { get; set; }
}
