using System;
using System.Collections.Generic;

namespace Domain.Models;

public partial class DocumentIndex
{
    public int IndexId { get; set; }

    public int DocumentId { get; set; }

    public int ModelId { get; set; }

    public int StrategyId { get; set; }

    public int ChunkSize { get; set; }

    public int ChunkOverlap { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Document Document { get; set; } = null!;

    public virtual ICollection<DocumentChunk> DocumentChunks { get; set; } = new List<DocumentChunk>();

    public virtual EmbeddingModel Model { get; set; } = null!;

    public virtual ChunkingStrategy Strategy { get; set; } = null!;
}
