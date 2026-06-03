using System.Collections.Generic;

namespace BusinessLayer.DTOs
{
    public class ChunkingStrategyDto
    {
        public int StrategyId { get; set; }
        public string StrategyName { get; set; } = null!;

        public virtual ICollection<DocumentIndexDto> DocumentIndices { get; set; } = new List<DocumentIndexDto>();
        public virtual ICollection<ExperimentDto> Experiments { get; set; } = new List<ExperimentDto>();
    }
}
