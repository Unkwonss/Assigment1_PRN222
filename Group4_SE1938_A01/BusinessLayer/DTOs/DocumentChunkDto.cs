using System.Collections.Generic;

namespace BusinessLayer.DTOs
{
    public class DocumentChunkDto
    {
        public int ChunkId { get; set; }
        public int IndexId { get; set; }
        public int ChunkOrder { get; set; }
        public string Content { get; set; } = null!;
        public int? PageNumber { get; set; }
        public int TokenCount { get; set; }
        public string VectorStoreKey { get; set; } = null!;
        public string? EmbeddingVector { get; set; }
        public bool HasEmbedding { get; set; }

        public virtual ICollection<ChatCitationDto> ChatCitations { get; set; } = new List<ChatCitationDto>();
        public virtual DocumentIndexDto? Index { get; set; }
    }
}
