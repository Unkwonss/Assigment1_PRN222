using System.Collections.Generic;

namespace BusinessLayer.DTOs
{
    public class EmbeddingModelDto
    {
        public int ModelId { get; set; }
        public string ModelName { get; set; } = null!;
        public string Provider { get; set; } = null!;

        public virtual ICollection<DocumentIndexDto> DocumentIndices { get; set; } = new List<DocumentIndexDto>();
        public virtual ICollection<ExperimentDto> Experiments { get; set; } = new List<ExperimentDto>();
    }
}
