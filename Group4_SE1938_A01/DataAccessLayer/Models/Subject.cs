using System;
using System.Collections.Generic;

namespace Domain.Models;

public partial class Subject
{
    public int SubjectId { get; set; }

    public string SubjectCode { get; set; } = null!;

    public string SubjectName { get; set; } = null!;

    public virtual ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();

    public virtual ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();

    public virtual ICollection<TestSet> TestSets { get; set; } = new List<TestSet>();
}
