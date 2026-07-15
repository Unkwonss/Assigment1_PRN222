using System;
using System.Collections.Generic;

namespace Domain.Models;

public partial class User
{
    public int UserId { get; set; }

    public string Username { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Role { get; set; } = null!;

    public int WeeklyTokenLimit { get; set; } = 250000;

    public int PurchasedTokenBalance { get; set; } = 0;

    public DateTime? PurchasedTokenExpiry { get; set; }

    public virtual ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();

    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
}
