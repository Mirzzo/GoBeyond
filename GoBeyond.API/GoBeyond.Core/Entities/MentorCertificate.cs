namespace GoBeyond.Core.Entities;

public class MentorCertificate : BaseEntity
{
    public int MentorProfileId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public bool IsVerified { get; set; }
    public DateTime? VerifiedAt { get; set; }

    public MentorProfile MentorProfile { get; set; } = null!;
}
