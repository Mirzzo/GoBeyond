using System.Globalization;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Infrastructure.Common;
using GoBeyond.Infrastructure.Configuration;
using GoBeyond.Infrastructure.Services.Subscriptions;
using GoBeyond.Tests.Payments;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GoBeyond.Tests.Subscriptions;

/// <summary>
/// Tekstovi obavijesti i emailova: datum u vremenskoj zoni platforme (kao u aplikacijama), bez dvostruke tačke iza
/// datuma, razlog uvijek završava rečenicom, rodno neutralne formulacije i iznos povrata iz stvarno vraćenih uplata.
/// </summary>
public sealed class SubscriptionTextTests
{
    private static readonly string[] MasculineOnlyVerbs = ["je prihvatio", "je odbio", "je uplatio", "je produžio"];

    private readonly FakePaymentGateway _gateway = new();
    private readonly RecordingNotificationSender _notifications = new();
    private readonly SubscriptionWorkflow _workflow;

    public SubscriptionTextTests()
    {
        _workflow = new SubscriptionWorkflow(_notifications, _gateway,
            Options.Create(new LifecycleOptions { SubscriptionPeriodDays = 30 }), NullLogger<SubscriptionWorkflow>.Instance);
    }

    [Theory]
    [InlineData("2026-11-28T23:30:00Z", "29.11.2026.")] // CET (UTC+1): u BiH je već sljedeći dan
    [InlineData("2026-10-01T22:30:00Z", "02.10.2026.")] // CEST (UTC+2)
    [InlineData("2026-10-01T21:59:00Z", "01.10.2026.")]
    [InlineData("2026-06-15T10:00:00Z", "15.06.2026.")]
    public void Date_IsShownInThePlatformTimeZone(string utc, string expected)
    {
        var value = DateTime.Parse(utc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

        Assert.Equal(expected, DomainTexts.Date(value));
        // Vrijednost iz baze (datetime2) nema Kind - tretira se kao UTC.
        Assert.Equal(expected, DomainTexts.Date(DateTime.SpecifyKind(value, DateTimeKind.Unspecified)));
    }

    [Fact]
    public void Date_WithoutValue_IsDash() => Assert.Equal("-", DomainTexts.Date(null));

    [Fact]
    public void Date_UsesTheConfiguredTimeZone() =>
        Assert.Equal("28.11.2026.", DomainTexts.Date(new DateTime(2026, 11, 28, 23, 30, 0, DateTimeKind.Utc), "UTC"));

    [Theory]
    [InlineData("Trenutno nemam slobodnih termina", "Trenutno nemam slobodnih termina.")]
    [InlineData("Trenutno nemam slobodnih termina.  ", "Trenutno nemam slobodnih termina.")]
    [InlineData("Da li ste sigurni?", "Da li ste sigurni?")]
    [InlineData("Hitno!", "Hitno!")]
    public void Sentence_AlwaysEndsWithPunctuation(string text, string expected) => Assert.Equal(expected, DomainTexts.Sentence(text));

    [Fact]
    public async Task RenewalTexts_UseLocalDateWithASinglePeriodAndNeutralWording()
    {
        var subscription = NewSubscription(SubscriptionStatus.Active);
        subscription.EndDate = new DateTime(2026, 10, 29, 23, 30, 0, DateTimeKind.Utc);
        var payment = AddPayment(subscription, PaymentPurpose.Renewal, 29.99m, PaymentStatus.Pending);

        await _workflow.ApplySuccessfulPaymentAsync(payment, new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc), CancellationToken.None);

        var client = _notifications.Sent.Single(x => x.UserId == 11);
        var mentor = _notifications.Sent.Single(x => x.UserId == 22);
        Assert.Equal("Uplata od 29.99 USD je uspješna. Saradnja sa mentorom Selma Delić traje do 29.11.2026.", client.Body);
        Assert.Equal("Pretplata je produžena", mentor.Title);
        Assert.Equal("Saradnja sa klijentom Nađa Škrijelj je produžena do 29.11.2026.", mentor.Body);
        AssertCleanTexts();
    }

    [Fact]
    public void ExpiryTexts_HaveASinglePeriodAfterTheDate()
    {
        var subscription = NewSubscription(SubscriptionStatus.Active);
        subscription.EndDate = new DateTime(2026, 9, 28, 23, 30, 0, DateTimeKind.Utc);

        _workflow.Expire(subscription, new DateTime(2026, 9, 29, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal("Pretplata kod mentora Selma Delić je istekla 29.09.2026. Vaš plan ostaje dostupan u historiji, " +
                     "a saradnju možete obnoviti novom pretplatom.", _notifications.Sent.Single(x => x.UserId == 11).Body);
        Assert.Equal("Pretplata klijenta Nađa Škrijelj je istekla 29.09.2026.", _notifications.Sent.Single(x => x.UserId == 22).Body);
        AssertCleanTexts();
    }

    [Fact]
    public async Task InitialPaymentAndAcceptTexts_AreGenderNeutral()
    {
        var subscription = NewSubscription(SubscriptionStatus.PendingPayment);
        var payment = AddPayment(subscription, PaymentPurpose.Initial, 29.99m, PaymentStatus.Pending);
        var now = new DateTime(2026, 9, 29, 23, 30, 0, DateTimeKind.Utc); // kraj 29.10. 23:30 UTC = 30.10. u BiH

        await _workflow.ApplySuccessfulPaymentAsync(payment, now, CancellationToken.None);
        _workflow.Accept(subscription, now);

        Assert.Contains(_notifications.Sent, x => x.UserId == 22 && x.Type == NotificationType.NewCollaborationRequest &&
                                                  x.Body.Contains("Nađa Škrijelj"));
        var accepted = _notifications.Sent.Single(x => x.Type == NotificationType.RequestAccepted);
        Assert.Equal("Vaš zahtjev za saradnju sa mentorom Selma Delić je prihvaćen. Saradnja traje do 30.10.2026., " +
                     "a trening plan ćete dobiti uskoro.", accepted.Body);
        AssertCleanTexts();
    }

    [Fact]
    public async Task RejectText_EndsTheReasonWithAPeriodAndStatesTheAmountActuallyRefunded()
    {
        // Mentor je u međuvremenu promijenio cijenu (26.00), a klijent je platio staru (27.50).
        var subscription = NewSubscription(SubscriptionStatus.AwaitingMentor);
        subscription.Price = 26.00m;
        AddPayment(subscription, PaymentPurpose.Initial, 27.50m, PaymentStatus.Succeeded);

        await _workflow.RejectAsync(subscription, "Trenutno nemam slobodnih termina", DateTime.UtcNow, CancellationToken.None);

        var rejected = Assert.Single(_notifications.Sent);
        Assert.Equal("Vaš zahtjev za saradnju sa mentorom Selma Delić je odbijen. Razlog: Trenutno nemam slobodnih termina. " +
                     "Uplaćeni iznos od 27.50 USD biće vraćen na vašu karticu.", rejected.Body);
        AssertCleanTexts();
    }

    private void AssertCleanTexts()
    {
        foreach (var (_, _, title, body, _) in _notifications.Sent)
        {
            Assert.DoesNotContain("..", body);
            foreach (var verb in MasculineOnlyVerbs)
            {
                Assert.DoesNotContain(verb, body);
                Assert.DoesNotContain(verb, title);
            }
        }
    }

    private static Subscription NewSubscription(SubscriptionStatus status) => new()
    {
        Id = 5,
        Status = status,
        Price = 29.99m,
        Currency = "usd",
        ClientProfile = new ClientProfile { User = new User { Id = 11, FirstName = "Nađa", LastName = "Škrijelj", Email = "nadja@test.ba" } },
        MentorProfile = new MentorProfile { User = new User { Id = 22, FirstName = "Selma", LastName = "Delić", Email = "selma@test.ba" } }
    };

    private static Payment AddPayment(Subscription subscription, PaymentPurpose purpose, decimal amount, PaymentStatus status)
    {
        var payment = new Payment
        {
            Id = 7, SubscriptionId = subscription.Id, Subscription = subscription, Amount = amount, Currency = "usd",
            StripePaymentIntentId = "pi_text", Purpose = purpose, Status = status
        };
        subscription.Payments.Add(payment);
        return payment;
    }
}
