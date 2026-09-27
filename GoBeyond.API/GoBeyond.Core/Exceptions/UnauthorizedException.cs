namespace GoBeyond.Core.Exceptions;

/// <summary>401 - neispravni pristupni podaci ili istekla sesija.</summary>
public sealed class UnauthorizedException(string message) : DomainException(message);
