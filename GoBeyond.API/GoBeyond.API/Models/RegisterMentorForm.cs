using GoBeyond.Core.DTOs.Auth;

namespace GoBeyond.API.Models;

/// <summary>Multipart forma za registraciju mentora: polja iz RegisterMentorRequest + fajlovi certifikata.</summary>
public sealed class RegisterMentorForm : RegisterMentorRequest
{
    public List<IFormFile> Certificates { get; set; } = [];
}
