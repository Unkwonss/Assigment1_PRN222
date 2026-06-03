namespace BusinessLayer.DTOs
{
    public class ChatCitationDto
    {
        public int CitationId { get; set; }
        public int HistoryId { get; set; }
        public int ChunkId { get; set; }
        public int? PageNumber { get; set; }
        public string? Snippet { get; set; }

        public virtual DocumentChunkDto? Chunk { get; set; }
        public virtual ChatHistoryDto? History { get; set; }
    }
}
