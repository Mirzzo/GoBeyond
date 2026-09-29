using GoBeyond.Contracts.Messages;
using GoBeyond.EmailConsumer.Services;

namespace GoBeyond.Tests.Email;

public class EmailIdempotencyKeyTests
{
    private static EmailNotificationMessage Message(
        int messageId = 7301, string eventType = "ClientRegistered", string recipient = "a@example.org",
        string subject = "Subject", string body = "Body", long createdAtTicks = 100) =>
        new(messageId, eventType, recipient, subject, body, createdAtTicks);

    [Fact]
    public void For_ReturnsSameKey_ForTwoIdenticalMessages()
    {
        // This is the true redelivery case: RabbitMQ republishes the exact same bytes after the consumer
        // crashed between the successful SMTP send and the ack. Same MessageId, same CreatedAt, same content
        // -> same key -> recognized as already sent (BG-06).
        Assert.Equal(EmailIdempotencyKey.For(Message()), EmailIdempotencyKey.For(Message()));
    }

    [Fact]
    public void For_ReturnsDifferentKey_WhenCreatedAtDiffers_EvenWithTheSameMessageId()
    {
        // Review major defect repro: simulates a database reset. OutboxMessages restarts from Id 1, so a
        // brand-new, unrelated email can get the SAME MessageId an old, already-sent email once had - but its
        // CreatedAt is necessarily different (it was created after the reset).
        var before = Message(messageId: 7301, createdAtTicks: 100);
        var after = Message(messageId: 7301, createdAtTicks: 999);

        Assert.NotEqual(EmailIdempotencyKey.For(before), EmailIdempotencyKey.For(after));
    }

    [Fact]
    public void For_ReturnsDifferentKey_WhenContentDiffers_EvenWithTheSameMessageIdAndCreatedAt()
    {
        // Review's exact runtime repro: MessageId 7301 reused with a different recipient/subject/body/event
        // type must NOT be treated as the same message, even if CreatedAtTicks happened to collide too.
        var first = Message(recipient: "rev.e1.first@example.org", subject: "dup A", body: "body A");
        var different = Message(eventType: "MentorApproved", recipient: "rev.e1.other.user@example.org",
            subject: "different email same id (after DB reset)", body: "different");

        Assert.NotEqual(EmailIdempotencyKey.For(first), EmailIdempotencyKey.For(different));
    }

    [Fact]
    public void For_ReturnsDifferentKey_ForDifferentMessageId_EvenWithIdenticalOtherFields()
    {
        var a = Message(messageId: 1);
        var b = Message(messageId: 2);

        Assert.NotEqual(EmailIdempotencyKey.For(a), EmailIdempotencyKey.For(b));
    }
}
