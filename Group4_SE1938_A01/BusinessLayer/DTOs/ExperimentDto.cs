using System.Collections.Generic;

namespace BusinessLayer.DTOs
{
    public class ExperimentDto
    {
        public int ExperimentId { get; set; }
        public string ExperimentName { get; set; } = null!;
        public string? ExperimentDescription { get; set; }
        public int AimodelId { get; set; }
        public int? EmbeddingModelId { get; set; }
        public int? StrategyId { get; set; }
        public int? ChunkSize { get; set; }
        public int? ChunkOverlap { get; set; }

        public virtual AimodelDto? Aimodel { get; set; }
        public virtual ICollection<BenchmarkResultDto> BenchmarkResults { get; set; } = new List<BenchmarkResultDto>();
        public virtual EmbeddingModelDto? EmbeddingModel { get; set; }
        public virtual ChunkingStrategyDto? Strategy { get; set; }
    }
}
