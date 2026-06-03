using System;
using System.Collections.Generic;

namespace Domain.Models;

public partial class DocumentChunk
{
    public int ChunkId { get; set; }

    public int IndexId { get; set; }

    public int ChunkOrder { get; set; }

    public string Content { get; set; } = null!;

    public int? PageNumber { get; set; }

    public int TokenCount { get; set; }

    public string VectorStoreKey { get; set; } = null!;

    // JSON serialized embedding vector
    public string? EmbeddingVector { get; set; }

    public bool HasEmbedding { get; set; }

    public virtual ICollection<ChatCitation> ChatCitations { get; set; } = new List<ChatCitation>();

    public virtual DocumentIndex Index { get; set; } = null!;
}
