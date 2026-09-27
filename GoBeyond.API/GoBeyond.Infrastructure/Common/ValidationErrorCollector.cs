using GoBeyond.Core.Exceptions;

namespace GoBeyond.Infrastructure.Common;

/// <summary>
/// Skuplja greške po poljima (camelCase ključevi) iz poslovnih provjera (npr. jedinstvenost,
/// postojanje šifarnika) i baca jedan <see cref="ValidationException"/> sa svima odjednom.
/// </summary>
public sealed class ValidationErrorCollector
{
    private readonly Dictionary<string, List<string>> _errors = new();

    public bool HasErrors => _errors.Count > 0;

    public void Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var list)) _errors[field] = list = [];
        list.Add(message);
    }

    /// <summary>Dodaje grešku ako uslov NIJE ispunjen.</summary>
    public void Require(bool condition, string field, string message)
    {
        if (!condition) Add(field, message);
    }

    public async Task RequireAsync(Task<bool> condition, string field, string message)
    {
        if (!await condition) Add(field, message);
    }

    public void ThrowIfAny()
    {
        if (HasErrors)
            throw new ValidationException(_errors.ToDictionary(x => x.Key, x => x.Value.ToArray()));
    }
}
