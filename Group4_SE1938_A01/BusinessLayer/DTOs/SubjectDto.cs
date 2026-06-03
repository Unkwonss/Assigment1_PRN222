using System.Collections.Generic;

namespace BusinessLayer.DTOs
{
    public class SubjectDto
    {
        public int SubjectId { get; set; }
        public string SubjectCode { get; set; } = null!;
        public string SubjectName { get; set; } = null!;
        
        public int? ManagedByUserId { get; set; }
        public string? ManagedByUserName { get; set; }
        
        public List<int> AssignedTeacherIds { get; set; } = new List<int>();
        public List<UserDto> AssignedTeachers { get; set; } = new List<UserDto>();

        public virtual ICollection<ChapterDto> Chapters { get; set; } = new List<ChapterDto>();
        public virtual ICollection<ChatSessionDto> ChatSessions { get; set; } = new List<ChatSessionDto>();
        public virtual ICollection<TestSetDto> TestSets { get; set; } = new List<TestSetDto>();
    }
}
