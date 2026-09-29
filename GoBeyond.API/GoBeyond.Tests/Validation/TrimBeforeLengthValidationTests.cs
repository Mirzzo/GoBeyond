using System.ComponentModel.DataAnnotations;
using GoBeyond.Core.DTOs.Admin;
using GoBeyond.Core.DTOs.Auth;
using GoBeyond.Core.DTOs.Communication;
using GoBeyond.Core.DTOs.Plans;
using GoBeyond.Core.DTOs.Profile;
using GoBeyond.Core.DTOs.Progress;
using GoBeyond.Core.DTOs.Subscriptions;
using GoBeyond.Infrastructure.Configuration;

namespace GoBeyond.Tests.Validation;

/// <summary>
/// B3 (PLN-02): slobodni tekst se mora trimovati PRIJE provjere minimalne dužine, inače razmacima
/// "napunjen" string prođe validaciju iako je stvarni (trimovani) sadržaj prekratak.
/// Kao dokaz da se vrijednost zaista trima (ne samo validira), svaki test i provjerava rezultujuću vrijednost.
/// </summary>
public class TrimBeforeLengthValidationTests
{
    private const string PaddedSingleChar = "          x          "; // trima se na "x" (dužina 1)

    private static bool IsPropertyValid<T>(T instance, string propertyName, out List<ValidationResult> results) where T : notnull
    {
        results = [];
        var property = typeof(T).GetProperty(propertyName)!;
        var value = property.GetValue(instance);
        var context = new ValidationContext(instance) { MemberName = propertyName };
        return Validator.TryValidateProperty(value, context, results);
    }

    private static void AssertAllStringPropertiesRejectPaddedSingleChar<T>(T instance) where T : notnull
    {
        foreach (var property in typeof(T).GetProperties().Where(p => p.PropertyType == typeof(string)))
        {
            property.SetValue(instance, PaddedSingleChar);
            Assert.False(IsPropertyValid(instance, property.Name, out _), $"{typeof(T).Name}.{property.Name} trebalo je odbiti razmacima napunjen string.");
        }
    }

    [Fact]
    public void RejectRequest_Reason_PaddedTooShort_FailsValidation()
    {
        var request = new RejectRequest { Reason = PaddedSingleChar };
        Assert.Equal("x", request.Reason); // trimovano prije snimanja
        Assert.False(IsPropertyValid(request, nameof(RejectRequest.Reason), out _));
    }

    [Fact]
    public void RejectRequest_Reason_PaddedButLongEnough_TrimsAndPasses()
    {
        var request = new RejectRequest { Reason = "   dovoljno dug razlog za odbijanje   " };
        Assert.Equal("dovoljno dug razlog za odbijanje", request.Reason);
        Assert.True(IsPropertyValid(request, nameof(RejectRequest.Reason), out _));
    }

    [Fact]
    public void CancelSubscriptionRequest_Reason_PaddedTooShort_FailsValidation()
    {
        var request = new CancelSubscriptionRequest { Reason = PaddedSingleChar };
        Assert.Equal("x", request.Reason);
        Assert.False(IsPropertyValid(request, nameof(CancelSubscriptionRequest.Reason), out _));
    }

    [Fact]
    public void UpdateAnnouncementRequest_TitleAndContent_PaddedTooShort_FailValidation()
    {
        var request = new UpdateAnnouncementRequest();
        AssertAllStringPropertiesRejectPaddedSingleChar(request);
    }

    [Fact]
    public void QuestionnaireRequest_AllFields_PaddedTooShort_FailValidation()
    {
        var request = new QuestionnaireRequest();
        AssertAllStringPropertiesRejectPaddedSingleChar(request);
    }

    [Fact]
    public void UpsertDayPlanRequest_Descriptions_PaddedTooShort_FailValidation()
    {
        var request = new UpsertDayPlanRequest();
        AssertAllStringPropertiesRejectPaddedSingleChar(request);
    }

    [Fact]
    public void UpsertProgressRequest_TextFields_PaddedTooShort_FailValidation()
    {
        var request = new UpsertProgressRequest();
        AssertAllStringPropertiesRejectPaddedSingleChar(request);
    }

    [Fact]
    public void CreateReviewRequest_Comment_PaddedTooShort_FailsValidation()
    {
        var request = new CreateReviewRequest { Comment = PaddedSingleChar };
        Assert.False(IsPropertyValid(request, nameof(CreateReviewRequest.Comment), out _));
    }

    [Fact]
    public void UpdateReviewRequest_Comment_PaddedTooShort_FailsValidation()
    {
        var request = new UpdateReviewRequest { Comment = PaddedSingleChar };
        Assert.False(IsPropertyValid(request, nameof(UpdateReviewRequest.Comment), out _));
    }

    [Fact]
    public void SendMessageRequest_Content_WhitespaceOnly_FailsValidation()
    {
        var request = new SendMessageRequest { Content = "          " };
        Assert.Equal(string.Empty, request.Content);
        Assert.False(IsPropertyValid(request, nameof(SendMessageRequest.Content), out _));
    }

    [Fact]
    public void MentorProfileRequest_Bio_PaddedTooShort_FailsValidation()
    {
        var request = new MentorProfileRequest { Bio = "   " + new string('a', 20) + "   " }; // trimovano 20 < min 50
        Assert.False(IsPropertyValid(request, nameof(MentorProfileRequest.Bio), out _));
    }

    [Fact]
    public void RegisterMentorRequest_Bio_PaddedTooShort_FailsValidation()
    {
        var request = new RegisterMentorRequest { Bio = "   " + new string('a', 20) + "   " };
        Assert.False(IsPropertyValid(request, nameof(RegisterMentorRequest.Bio), out _));
    }

    [Fact]
    public void CreatePlanRequest_MotivationalQuote_TrimsBeforeMaxLengthCheck()
    {
        // Sirova dužina (sa razmacima) prelazi 300, ali trimovan sadržaj staje - ranije bi ovo bilo pogrešno odbijeno.
        var raw = new string(' ', 20) + new string('m', 290) + new string(' ', 20);
        var request = new CreatePlanRequest { MotivationalQuote = raw };
        Assert.Equal(290, request.MotivationalQuote!.Length);
        Assert.True(IsPropertyValid(request, nameof(CreatePlanRequest.MotivationalQuote), out _));
    }
}

/// <summary>B3 (PAY-13): mjesečna cijena mentora smije imati najviše dvije decimale (kolona je decimal(10,2)).</summary>
public class MonthlyPricePrecisionTests
{
    private static bool IsPropertyValid<T>(T instance, string propertyName, out List<ValidationResult> results) where T : notnull
    {
        results = [];
        var property = typeof(T).GetProperty(propertyName)!;
        var value = property.GetValue(instance);
        var context = new ValidationContext(instance) { MemberName = propertyName };
        return Validator.TryValidateProperty(value, context, results);
    }

    [Theory]
    [InlineData(24.99, true)]
    [InlineData(25, true)]
    [InlineData(1, true)]
    [InlineData(24.999, false)]
    [InlineData(24.994, false)]
    public void MentorProfileRequest_MonthlyPrice_RejectsMoreThanTwoDecimals(decimal price, bool expectedValid)
    {
        var request = new MentorProfileRequest { MonthlyPrice = price };
        Assert.Equal(expectedValid, IsPropertyValid(request, nameof(MentorProfileRequest.MonthlyPrice), out _));
    }

    [Theory]
    [InlineData(24.99, true)]
    [InlineData(24.999, false)]
    public void RegisterMentorRequest_MonthlyPrice_RejectsMoreThanTwoDecimals(decimal price, bool expectedValid)
    {
        var request = new RegisterMentorRequest { MonthlyPrice = price };
        Assert.Equal(expectedValid, IsPropertyValid(request, nameof(RegisterMentorRequest.MonthlyPrice), out _));
    }
}

/// <summary>B3 (AUTH-10): poruka o prevelikom zahtjevu mora biti gramatički ispravna i generička (vrijedi i za endpointe sa jednim fajlom).</summary>
public class RequestTooLargeMessageTests
{
    [Fact]
    public void RequestTooLargeMessage_IsGenericSingleFileWording_WithoutBrokenPlural()
    {
        var options = new UploadOptions { MaxFileSizeBytes = 5 * 1024 * 1024, MaxCertificatesPerUpload = 5 };

        Assert.DoesNotContain("takva fajla", options.RequestTooLargeMessage);
        Assert.DoesNotContain("a zahtjev najviše", options.RequestTooLargeMessage);
        Assert.Contains("Pojedinačni fajl", options.RequestTooLargeMessage);
        Assert.Contains("5 MB", options.RequestTooLargeMessage);
    }
}
