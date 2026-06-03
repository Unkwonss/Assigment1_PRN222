using System;
using System.Collections.Generic;

namespace BusinessLayer.DTOs
{
    public class ChatHistoryDto
    {
        public int HistoryId { get; set; }
        public Guid SessionId { get; set; }
        public string UserMessage { get; set; } = null!;
        public string? StandaloneQuery { get; set; }
        public string BotResponse { get; set; } = null!;
        public DateTime? Timestamp { get; set; }

        public virtual ICollection<ChatCitationDto> ChatCitations { get; set; } = new List<ChatCitationDto>();
        public virtual ChatSessionDto? Session { get; set; }
    }
}
