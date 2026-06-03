using System;
using System.Collections.Generic;

namespace Domain.Models;

public partial class SubjectTeacher
{
    public int SubjectId { get; set; }
    public virtual Subject Subject { get; set; } = null!;

    public int UserId { get; set; }
    public virtual User User { get; set; } = null!;

    public bool IsSubjectHead { get; set; }
}
