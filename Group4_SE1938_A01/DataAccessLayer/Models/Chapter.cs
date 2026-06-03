using System;
using System.Collections.Generic;

namespace Domain.Models;

public partial class Chapter
{
    public int ChapterId { get; set; }

    public int SubjectId { get; set; }

    public int ChapterNumber { get; set; }

    public string ChapterName { get; set; } = null!;

    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();

    public virtual Subject Subject { get; set; } = null!;
}
