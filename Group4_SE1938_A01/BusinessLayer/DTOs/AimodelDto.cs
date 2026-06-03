using System.Collections.Generic;

namespace BusinessLayer.DTOs
{
    public class AimodelDto
    {
        public int ModelId { get; set; }
        public string ModelName { get; set; } = null!;
        public string ModelType { get; set; } = null!;

        public virtual ICollection<ExperimentDto> Experiments { get; set; } = new List<ExperimentDto>();
    }
}
