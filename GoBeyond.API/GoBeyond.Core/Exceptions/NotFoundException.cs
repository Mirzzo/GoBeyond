namespace GoBeyond.Core.Exceptions;

/// <summary>404 - traženi zapis ne postoji.</summary>
public sealed class NotFoundException(string message) : DomainException(message);
