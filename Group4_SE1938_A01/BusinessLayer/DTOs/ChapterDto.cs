using System.Collections.Generic;

namespace BusinessLayer.DTOs
{
    public class ChapterDto
    {
        public int ChapterId { get; set; }
        public int SubjectId { get; set; }
        public int ChapterNumber { get; set; }
        public string ChapterName { get; set; } = null!;

        public virtual ICollection<DocumentDto> Documents { get; set; } = new List<DocumentDto>();
        public virtual SubjectDto? Subject { get; set; }
    }
}
