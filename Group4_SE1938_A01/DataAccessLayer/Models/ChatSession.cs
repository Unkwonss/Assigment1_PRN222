using System;
using System.Collections.Generic;

namespace Domain.Models;

public partial class ChatSession
{
    public Guid SessionId { get; set; }

    public int UserId { get; set; }

    public int SubjectId { get; set; }

    public string? Title { get; set; }

    public string? ConversationSummary { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? LastUpdatedAt { get; set; }

    public virtual ICollection<ChatHistory> ChatHistories { get; set; } = new List<ChatHistory>();

    public virtual Subject Subject { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
