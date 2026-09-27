namespace GoBeyond.API.Validation;

/// <summary>Oblik greške 400 validacije: { "message": "...", "errors": { "polje": ["poruka"] } }.</summary>
public sealed record ValidationErrorResponse(string Message, IReadOnlyDictionary<string, string[]> Errors);
