using System;
using System.Collections.Generic;

namespace Domain.Models;

public partial class Subject
{
    public int SubjectId { get; set; }

    public string SubjectCode { get; set; } = null!;

    public string SubjectName { get; set; } = null!;

    public int? DefaultModelId { get; set; }

    public int? DefaultStrategyId { get; set; }

    public int? DefaultChunkSize { get; set; }

    public int? DefaultChunkOverlap { get; set; }

    public virtual ICollection<SubjectTeacher> SubjectTeachers { get; set; } = new List<SubjectTeacher>();

    public virtual ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();

    public virtual ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();

    public virtual ICollection<TestSet> TestSets { get; set; } = new List<TestSet>();
}
