using System.Globalization;
using System.Text.Json.Serialization;
using GoBeyond.API.Extensions;
using GoBeyond.API.Middleware;
using GoBeyond.API.Validation;
using GoBeyond.Contracts.Configuration;
using GoBeyond.Infrastructure.Configuration;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Extensions;
using Microsoft.AspNetCore.Http.Features;

// Brojevi i datumi se parsiraju nezavisno od jezika operativnog sistema (npr. "29.99").
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

// Jedini izvor konfiguracije: appsettings.Shared.json (pregaziv environment varijablama / .env).
builder.Configuration.AddSharedConfiguration();

builder.Services
    .AddControllers(options =>
    {
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
        ValidationResponseFactory.ConfigureMessages(options.ModelBindingMessageProvider);
    })
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = ValidationResponseFactory.Create);

var uploads = builder.Configuration.GetSection(UploadOptions.SectionName).Get<UploadOptions>() ?? new UploadOptions();
builder.Services.Configure<FormOptions>(options =>
    options.MultipartBodyLengthLimit = uploads.MaxFileSizeBytes * Math.Max(1, uploads.MaxCertificatesPerUpload) + 1024 * 1024);

builder.Services.AddGoBeyondInfrastructure(builder.Configuration);
builder.Services.AddGoBeyondAuthentication(builder.Configuration);
builder.Services.AddGoBeyondSwagger();

var app = builder.Build();

await app.InitializeDatabaseAsync();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseStatusCodePages(async context =>
{
    // Prazni 404/405/415 odgovori dobijaju poruku u obliku ugovora.
    var response = context.HttpContext.Response;
    var message = response.StatusCode switch
    {
        StatusCodes.Status404NotFound => ErrorMessages.RouteNotFound,
        StatusCodes.Status405MethodNotAllowed => ErrorMessages.MethodNotAllowed,
        StatusCodes.Status415UnsupportedMediaType => ErrorMessages.UnsupportedMediaType,
        _ => null
    };
    if (message is not null) await response.WriteAsJsonAsync(new ErrorResponse(message));
});

if (app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles(); // /seed/... i /uploads/... (wwwroot)
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers().RequireAuthorization(); // anonimno samo gdje piše [AllowAnonymous]
app.MapGet("/health", async (GoBeyondDbContext db, CancellationToken ct) =>
        await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "ok" }) : Results.StatusCode(503))
    .AllowAnonymous();

await app.RunAsync();
