using System.Text.RegularExpressions;
using GoBeyond.Core.Exceptions;
using GoBeyond.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace GoBeyond.API.Validation;

/// <summary>
/// ApiBehaviorOptions.InvalidModelStateResponseFactory: DataAnnotations i model-binding greške vraća
/// u obliku ugovora { message, errors } sa camelCase ključevima (npr. "email", "mentor.bio",
/// "questionnaire.primaryGoal") i porukama na bosanskom.
/// </summary>
public static partial class ValidationResponseFactory
{
    private const string InvalidFormat = "Neispravan format vrijednosti.";

    public static IActionResult Create(ActionContext context)
    {
        var uploads = context.HttpContext.RequestServices.GetRequiredService<IOptions<UploadOptions>>().Value;
        var errors = new Dictionary<string, string[]>();
        foreach (var (key, entry) in context.ModelState)
        {
            if (entry.Errors.Count == 0) continue;
            var field = ToCamelCasePath(key);
            var messages = entry.Errors
                .Select(x => Translate(x.ErrorMessage, x.Exception, uploads))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToArray();
            if (messages.Length == 0) continue;
            errors[field] = errors.TryGetValue(field, out var existing) ? existing.Concat(messages).Distinct().ToArray() : messages;
        }

        // Kad tijelo zahtjeva nije ispravan JSON, ASP.NET dodaje i grešku za cijeli parametar - dovoljna je konkretna greška.
        if (errors.Count > 1) errors.Remove("body");

        // Prevelik multipart zahtjev: poruka o veličini ide i kao glavna poruka (nije vezana za jedno polje).
        var message = errors.Values.SelectMany(x => x).Contains(uploads.RequestTooLargeMessage)
            ? uploads.RequestTooLargeMessage
            : ValidationException.DefaultMessage;
        return new BadRequestObjectResult(new ValidationErrorResponse(message, errors));
    }

    /// <summary>Bosanske poruke za greške model binding-a (npr. "abc" za broj).</summary>
    public static void ConfigureMessages(DefaultModelBindingMessageProvider provider)
    {
        provider.SetAttemptedValueIsInvalidAccessor((value, _) => $"Vrijednost \"{value}\" nije ispravna.");
        provider.SetMissingBindRequiredValueAccessor(_ => "Vrijednost je obavezna.");
        provider.SetMissingKeyOrValueAccessor(() => "Vrijednost je obavezna.");
        provider.SetMissingRequestBodyRequiredValueAccessor(() => "Zahtjev mora sadržavati podatke.");
        provider.SetNonPropertyAttemptedValueIsInvalidAccessor(value => $"Vrijednost \"{value}\" nije ispravna.");
        provider.SetNonPropertyUnknownValueIsInvalidAccessor(() => "Vrijednost nije ispravna.");
        provider.SetNonPropertyValueMustBeANumberAccessor(() => "Vrijednost mora biti broj.");
        provider.SetUnknownValueIsInvalidAccessor(_ => "Vrijednost nije ispravna.");
        provider.SetValueIsInvalidAccessor(value => $"Vrijednost \"{value}\" nije ispravna.");
        provider.SetValueMustBeANumberAccessor(_ => "Vrijednost mora biti broj.");
        provider.SetValueMustNotBeNullAccessor(_ => "Vrijednost je obavezna.");
    }

    /// <summary>"$.Mentor.SpecializationIds[0]" → "mentor.specializationIds[0]"; prazan ključ → "body".</summary>
    public static string ToCamelCasePath(string key)
    {
        var path = key.TrimStart('$').TrimStart('.');
        if (string.IsNullOrEmpty(path) || path.Equals("request", StringComparison.OrdinalIgnoreCase)) return "body";
        return string.Join('.', path.Split('.').Select(segment =>
            segment.Length == 0 ? segment : char.ToLowerInvariant(segment[0]) + segment[1..]));
    }

    public static string Translate(string message, Exception? exception, UploadOptions uploads)
    {
        if (string.IsNullOrWhiteSpace(message)) return exception is null ? string.Empty : InvalidFormat;
        // Kestrel/FormOptions limiti ("Failed to read the request form. ... limit ... exceeded / too large").
        if (message.StartsWith("Failed to read the request form", StringComparison.Ordinal))
            return message.Contains("exceeded", StringComparison.Ordinal) || message.Contains("too large", StringComparison.Ordinal)
                ? uploads.RequestTooLargeMessage
                : "Zahtjev nije ispravan multipart/form-data.";
        if (message.StartsWith("The JSON value", StringComparison.Ordinal) || message.Contains("Path: $", StringComparison.Ordinal) ||
            message.StartsWith("'", StringComparison.Ordinal) || message.Contains("JSON", StringComparison.Ordinal))
            return InvalidFormat;
        if (message == "A non-empty request body is required.") return "Zahtjev mora sadržavati podatke.";
        if (RequiredFieldPattern().IsMatch(message)) return "Polje je obavezno.";
        return message;
    }

    [GeneratedRegex(@"^The .+ field is required\.$")]
    private static partial Regex RequiredFieldPattern();
}
