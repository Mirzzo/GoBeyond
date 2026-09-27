namespace GoBeyond.Core.Exceptions;

/// <summary>403 - korisnik nema pravo na traženu akciju.</summary>
public sealed class ForbiddenException(string message) : DomainException(message);
