namespace GoBeyond.Core.Entities;

public class MentorCertificate : BaseEntity
{
    public int MentorProfileId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;

    public MentorProfile MentorProfile { get; set; } = null!;
}
