using System;
using System.Collections.Generic;

namespace BusinessLayer.DTOs
{
    public class ChatSessionDto
    {
        public Guid SessionId { get; set; }
        public int UserId { get; set; }
        public int SubjectId { get; set; }
        public string? Title { get; set; }
        public string? ConversationSummary { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? LastUpdatedAt { get; set; }

        public virtual ICollection<ChatHistoryDto> ChatHistories { get; set; } = new List<ChatHistoryDto>();
        public virtual SubjectDto? Subject { get; set; }
        public virtual UserDto? User { get; set; }
    }
}
