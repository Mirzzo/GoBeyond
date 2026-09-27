namespace GoBeyond.Core.Exceptions;

/// <summary>409 - konflikt sa postojećim stanjem (duplikat, zapis u upotrebi...).</summary>
public sealed class ConflictException(string message) : DomainException(message);
