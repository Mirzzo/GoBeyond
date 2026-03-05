using GoBeyond.Core.Enums;

namespace GoBeyond.Core.DTOs.Auth;

public record RegisterMentorRequestDto(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string Bio,
    int Age,
    MentorCategory Category,
    decimal Price,
    string CertificateFileName,
    string CertificateFileUrl
);
