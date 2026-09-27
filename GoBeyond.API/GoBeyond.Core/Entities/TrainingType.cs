namespace GoBeyond.Core.Entities;

public class TrainingType : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ICollection<MentorProfile> Mentors { get; set; } = new List<MentorProfile>();
}
