using System.Reflection;
using System.Text.Json;
using GoBeyond.API.Middleware;
using GoBeyond.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GoBeyond.Tests.Security;

/// <summary>
/// Zahtjev koji SQL Server prekine zbog istovremene izmjene istih podataka (žrtva deadlock-a 1205, istek čekanja na
/// zaključavanje 1222) dobija 400 sa porukom da pokuša ponovo, i kad EF Core grešku umota (DbUpdateException pri SaveChanges,
/// InvalidOperationException za prolaznu grešku). Ostale greške baze su i dalje 500.
/// </summary>
public sealed class LockConflictResponseTests
{
    [Theory]
    [InlineData(1205, "direct")]
    [InlineData(1205, "save-changes")]
    [InlineData(1205, "transient")]
    [InlineData(1222, "direct")]
    public async Task SqlServerLockConflict_Returns400WithRetryMessage(int number, string wrapping)
    {
        var sql = CreateSqlException(number);
        Exception thrown = wrapping switch
        {
            "save-changes" => new DbUpdateException("An error occurred while saving the entity changes.", sql),
            "transient" => new InvalidOperationException("An exception has been raised that is likely due to a transient failure.", sql),
            _ => sql
        };

        var (status, body) = await RunAsync(thrown);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal(GlobalExceptionMiddleware.ConcurrentChangeMessage, body.Message);
        Assert.Equal("Isti podaci se upravo mijenjaju u drugom zahtjevu. Pokušajte ponovo.", body.Message);
    }

    [Fact]
    public async Task OtherSqlServerError_StillReturns500()
    {
        var (status, body) = await RunAsync(new DbUpdateException("Unique index violation.", CreateSqlException(2627)));

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.Equal(GlobalExceptionMiddleware.ServerErrorMessage, body.Message);
    }

    private static async Task<(int Status, ErrorBody Body)> RunAsync(Exception exception)
    {
        var middleware = new GlobalExceptionMiddleware(_ => throw exception, Options.Create(new UploadOptions()),
            NullLogger<GlobalExceptionMiddleware>.Instance);
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        return (context.Response.StatusCode, (await JsonSerializer.DeserializeAsync<ErrorBody>(context.Response.Body, SecurityTestApi.Json))!);
    }

    /// <summary>SqlException nema javni konstruktor: gradi se kao što ga gradi SqlClient (SqlError u SqlErrorCollection).</summary>
    private static SqlException CreateSqlException(int number)
    {
        const BindingFlags Internal = BindingFlags.NonPublic | BindingFlags.Instance;
        var errorConstructor = typeof(SqlError).GetConstructors(Internal).OrderByDescending(x => x.GetParameters().Length).First();
        var error = errorConstructor.Invoke(errorConstructor.GetParameters().Select(parameter => parameter.Name switch
        {
            "infoNumber" => number,
            _ when parameter.ParameterType == typeof(string) => $"SQL greška {number}",
            _ when parameter.ParameterType == typeof(Exception) => null,
            _ => Activator.CreateInstance(parameter.ParameterType)
        }).ToArray());

        var errors = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true)!;
        typeof(SqlErrorCollection).GetMethod("Add", Internal)!.Invoke(errors, [error]);
        var create = typeof(SqlException).GetMethod("CreateException", BindingFlags.NonPublic | BindingFlags.Static,
            [typeof(SqlErrorCollection), typeof(string)])!;
        var exception = (SqlException)create.Invoke(null, [errors, "16.00.0000"])!;
        Assert.Equal(number, exception.Number);
        return exception;
    }
}
