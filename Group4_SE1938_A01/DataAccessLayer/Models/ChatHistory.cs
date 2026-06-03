using System;
using System.Collections.Generic;

namespace Domain.Models;

public partial class ChatHistory
{
    public int HistoryId { get; set; }

    public Guid SessionId { get; set; }

    public string UserMessage { get; set; } = null!;

    public string? StandaloneQuery { get; set; }

    public string BotResponse { get; set; } = null!;

    public DateTime? Timestamp { get; set; }

    public virtual ICollection<ChatCitation> ChatCitations { get; set; } = new List<ChatCitation>();

    public virtual ChatSession Session { get; set; } = null!;
}
