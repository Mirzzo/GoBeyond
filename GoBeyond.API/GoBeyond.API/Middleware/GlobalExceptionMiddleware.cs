using GoBeyond.API.Validation;
using GoBeyond.Core.Exceptions;
using GoBeyond.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace GoBeyond.API.Middleware;

/// <summary>
/// Mapira domenske izuzetke na HTTP status i jedinstven oblik odgovora iz API ugovora:
/// 400 { message, errors }, 401/403/404/409 { message }, 500 { message }.
/// </summary>
public sealed class GlobalExceptionMiddleware(
    RequestDelegate next,
    IOptions<UploadOptions> uploadOptions,
    ILogger<GlobalExceptionMiddleware> logger)
{
    public const string ServerErrorMessage = "Došlo je do greške na serveru. Pokušajte ponovo.";

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
        _ => (StatusCodes.Status500InternalServerError, new ErrorResponse(ServerErrorMessage))
    };
}
