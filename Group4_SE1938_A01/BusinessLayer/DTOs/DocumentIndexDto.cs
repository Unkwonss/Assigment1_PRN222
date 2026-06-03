using System;
using System.Collections.Generic;

namespace BusinessLayer.DTOs
{
    public class DocumentIndexDto
    {
        public int IndexId { get; set; }
        public int DocumentId { get; set; }
        public int ModelId { get; set; }
        public int StrategyId { get; set; }
        public int ChunkSize { get; set; }
        public int ChunkOverlap { get; set; }
        public DateTime? CreatedAt { get; set; }

        public virtual DocumentDto? Document { get; set; }
        public virtual ICollection<DocumentChunkDto> DocumentChunks { get; set; } = new List<DocumentChunkDto>();
        public virtual EmbeddingModelDto? Model { get; set; }
        public virtual ChunkingStrategyDto? Strategy { get; set; }
    }
}
