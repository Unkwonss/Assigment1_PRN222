using System;
using System.Collections.Generic;

namespace Domain.Models;

public partial class Document
{
    public int DocumentId { get; set; }

    public int ChapterId { get; set; }

    public string Title { get; set; } = null!;

    public string FileName { get; set; } = null!;

    public string FilePath { get; set; } = null!;

    public string FileType { get; set; } = null!;

    public long FileSize { get; set; }

    public int TotalPages { get; set; }

    public string Status { get; set; } = null!;

    public int UploadedBy { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Chapter Chapter { get; set; } = null!;

    public virtual ICollection<DocumentIndex> DocumentIndices { get; set; } = new List<DocumentIndex>();

    public virtual User UploadedByNavigation { get; set; } = null!;
}
