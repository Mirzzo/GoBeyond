namespace GoBeyond.Core.Files;

/// <summary>Fajl primljen od klijenta, nezavisan od ASP.NET-a (IFormFile se mapira u kontroleru).</summary>
public sealed record FileUpload(string FileName, string ContentType, long Length, Func<Stream> OpenReadStream);
