using GoBeyond.Contracts.Messages;
using GoBeyond.EmailConsumer.Services;

namespace GoBeyond.Tests.Email;

public class NotificationMessageValidatorTests
{
    private static EmailNotificationMessage Valid() =>
        new(MessageId: 9007, EventType: "ClientRegistered", RecipientEmail: "klijent@gobeyond.ba",
            Subject: "Dobrodošli na GoBeyond", Body: "Hvala na registraciji.");

    [Fact]
    public void IsValid_ReturnsTrue_ForCompleteMessage()
    {
        Assert.True(NotificationMessageValidator.IsValid(Valid()));
    }

    [Fact]
    public void IsValid_ReturnsFalse_ForNullMessage()
    {
        Assert.False(NotificationMessageValidator.IsValid(null));
    }

    // A queue payload with only MessageId + RecipientEmail (no Subject/Body/EventType - e.g. published by hand,
    // bypassing the API outbox) must go to the DLQ instead of being sent as a blank email.
    [Theory]
    [InlineData("", "Hvala na registraciji.", "ClientRegistered")]
    [InlineData("   ", "Hvala na registraciji.", "ClientRegistered")]
    [InlineData(null, "Hvala na registraciji.", "ClientRegistered")]
    [InlineData("Dobrodošli na GoBeyond", "", "ClientRegistered")]
    [InlineData("Dobrodošli na GoBeyond", "   ", "ClientRegistered")]
    [InlineData("Dobrodošli na GoBeyond", null, "ClientRegistered")]
    [InlineData("Dobrodošli na GoBeyond", "Hvala na registraciji.", "")]
    [InlineData("Dobrodošli na GoBeyond", "Hvala na registraciji.", null)]
    public void IsValid_ReturnsFalse_WhenSubjectBodyOrEventTypeMissing(string? subject, string? body, string? eventType)
    {
        var message = new EmailNotificationMessage(9007, eventType!, "klijent@gobeyond.ba", subject!, body!);
        Assert.False(NotificationMessageValidator.IsValid(message));
    }

    [Fact]
    public void IsValid_ReturnsFalse_ForMissingRecipient()
    {
        var message = Valid() with { RecipientEmail = "" };
        Assert.False(NotificationMessageValidator.IsValid(message));
    }

    [Fact]
    public void IsValid_ReturnsFalse_ForNonPositiveMessageId()
    {
        var message = Valid() with { MessageId = 0 };
        Assert.False(NotificationMessageValidator.IsValid(message));
    }
}
