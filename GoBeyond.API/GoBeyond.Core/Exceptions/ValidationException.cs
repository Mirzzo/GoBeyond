namespace GoBeyond.Core.Exceptions;

/// <summary>400 - neispravan unos ili prekršeno poslovno pravilo. Opciono nosi greške po poljima.</summary>
public sealed class ValidationException : DomainException
{
    public const string DefaultMessage = "Provjerite unesene podatke.";

    public ValidationException(string message) : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(string field, string error) : base(DefaultMessage)
    {
        Errors = new Dictionary<string, string[]> { [field] = [error] };
    }

    public ValidationException(IDictionary<string, string[]> errors) : base(DefaultMessage)
    {
        Errors = new Dictionary<string, string[]>(errors);
    }

    /// <summary>Ključevi su camelCase nazivi polja.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
