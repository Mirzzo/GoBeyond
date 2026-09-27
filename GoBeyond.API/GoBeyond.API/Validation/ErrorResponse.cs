namespace GoBeyond.API.Validation;

/// <summary>Oblik greške 401/403/404/409/500: { "message": "..." }.</summary>
public sealed record ErrorResponse(string Message);
