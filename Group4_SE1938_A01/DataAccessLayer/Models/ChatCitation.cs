using System;
using System.Collections.Generic;

namespace Domain.Models;

public partial class ChatCitation
{
    public int CitationId { get; set; }

    public int HistoryId { get; set; }

    public int ChunkId { get; set; }

    public int? PageNumber { get; set; }

    public string? Snippet { get; set; }

    public virtual DocumentChunk Chunk { get; set; } = null!;

    public virtual ChatHistory History { get; set; } = null!;
}
