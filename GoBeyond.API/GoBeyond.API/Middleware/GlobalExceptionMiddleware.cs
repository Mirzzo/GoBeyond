using GoBeyond.API.Validation;
using GoBeyond.Core.Exceptions;
using GoBeyond.Infrastructure.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace GoBeyond.API.Middleware;

/// <summary>
/// Mapira domenske izuzetke na HTTP status i jedinstven oblik odgovora iz API ugovora:
/// 400 { message, errors }, 401/403/404/409 { message }, 500 { message }. Zahtjev koji SQL Server prekine zbog istovremene
/// izmjene istih podataka (deadlock, istek čekanja na zaključavanje) je 400 sa porukom da se pokuša ponovo, ne 500.
/// </summary>
public sealed class GlobalExceptionMiddleware(
    RequestDelegate next,
    IOptions<UploadOptions> uploadOptions,
    ILogger<GlobalExceptionMiddleware> logger)
{
    public const string ServerErrorMessage = "Došlo je do greške na serveru. Pokušajte ponovo.";
    public const string ConcurrentChangeMessage = "Isti podaci se upravo mijenjaju u drugom zahtjevu. Pokušajte ponovo.";

    /// <summary>SQL Server greške 1205 (transakcija izabrana kao žrtva deadlock-a) i 1222 (istek čekanja na zaključavanje).</summary>
    private static readonly int[] LockConflictErrors = [1205, 1222];

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // klijent je prekinuo zahtjev
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            var (status, body) = Map(ex);
            if (status >= 500)
                logger.LogError(ex, "Unhandled exception for {Method} {Path}.", context.Request.Method, context.Request.Path);
            else if (IsLockConflict(ex))
                logger.LogWarning(ex, "{Method} {Path} -> {Status}: SQL Server lock conflict.", context.Request.Method, context.Request.Path, status);
            else
                logger.LogInformation("{Method} {Path} -> {Status}: {Message}", context.Request.Method, context.Request.Path, status, ex.Message);

            context.Response.Clear();
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(body, body.GetType());
        }
    }

    private (int Status, object Body) Map(Exception exception) => exception switch
    {
        ValidationException ex when ex.Errors.Count > 0 =>
            (StatusCodes.Status400BadRequest, new ValidationErrorResponse(ex.Message, ex.Errors)),
        ValidationException ex => (StatusCodes.Status400BadRequest, new ErrorResponse(ex.Message)),
        NotFoundException ex => (StatusCodes.Status404NotFound, new ErrorResponse(ex.Message)),
        ConflictException ex => (StatusCodes.Status409Conflict, new ErrorResponse(ex.Message)),
        ForbiddenException ex => (StatusCodes.Status403Forbidden, new ErrorResponse(ex.Message)),
        UnauthorizedException ex => (StatusCodes.Status401Unauthorized, new ErrorResponse(ex.Message)),
        UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, new ErrorResponse(ErrorMessages.Unauthorized)),
        BadHttpRequestException ex when ex.StatusCode == StatusCodes.Status413PayloadTooLarge =>
            (StatusCodes.Status400BadRequest, new ErrorResponse(uploadOptions.Value.RequestTooLargeMessage)),
        BadHttpRequestException => (StatusCodes.Status400BadRequest, new ErrorResponse("Zahtjev nije ispravnog formata.")),
        _ when IsLockConflict(exception) => (StatusCodes.Status400BadRequest, new ErrorResponse(ConcurrentChangeMessage)),
        _ => (StatusCodes.Status500InternalServerError, new ErrorResponse(ServerErrorMessage))
    };

    /// <summary>EF Core grešku baze prosljeđuje direktno (ExecuteUpdate) ili umotanu (DbUpdateException pri SaveChanges).</summary>
    private static bool IsLockConflict(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
            if (current is SqlException sql && LockConflictErrors.Contains(sql.Number))
                return true;
        return false;
    }
}
