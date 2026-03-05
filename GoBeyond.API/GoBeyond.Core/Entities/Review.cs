namespace GoBeyond.Core.Entities;

public class Review : BaseEntity
{
    public int ClientProfileId { get; set; }
    public int MentorProfileId { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;

    public ClientProfile ClientProfile { get; set; } = null!;
    public MentorProfile MentorProfile { get; set; } = null!;
}
