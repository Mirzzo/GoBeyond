namespace GoBeyond.Core.Exceptions;

/// <summary>
/// Bazna domenska greška. GlobalExceptionMiddleware je mapira na HTTP status i
/// odgovor oblika <c>{ "message": "..." }</c> (poruke su na bosanskom).
/// </summary>
public abstract class DomainException(string message) : Exception(message);
