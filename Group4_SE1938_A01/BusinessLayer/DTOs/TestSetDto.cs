using System;
using System.Collections.Generic;

namespace BusinessLayer.DTOs
{
    public class TestSetDto
    {
        public int QuestionId { get; set; }
        public int SubjectId { get; set; }
        public string Question { get; set; } = null!;
        public string GroundTruth { get; set; } = null!;
        public DateTime? CreatedAt { get; set; }

        public virtual ICollection<BenchmarkResultDto> BenchmarkResults { get; set; } = new List<BenchmarkResultDto>();
        public virtual SubjectDto? Subject { get; set; }
    }
}
