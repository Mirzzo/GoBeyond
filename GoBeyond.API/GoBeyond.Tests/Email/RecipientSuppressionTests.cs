using GoBeyond.EmailConsumer.Services;

namespace GoBeyond.Tests.Email;

public class RecipientSuppressionTests
{
    private static readonly string[] SuppressedDomains = ["gobeyond.ba"];

    [Theory]
    [InlineData("mailpit")]
    [InlineData("MAILPIT")]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    public void ShouldSuppress_NeverSuppresses_WhenHostIsMailpit(string host)
    {
        Assert.False(RecipientSuppression.ShouldSuppress(host, "mentor@gobeyond.ba", SuppressedDomains));
    }

    [Fact]
    public void ShouldSuppress_SuppressesConfiguredDomain_WhenHostIsRealSmtp()
    {
        Assert.True(RecipientSuppression.ShouldSuppress("smtp.gmail.com", "mentor@gobeyond.ba", SuppressedDomains));
    }

    [Fact]
    public void ShouldSuppress_IsCaseInsensitiveOnDomain()
    {
        Assert.True(RecipientSuppression.ShouldSuppress("smtp.gmail.com", "mentor@GoBeyond.BA", SuppressedDomains));
    }

    [Fact]
    public void ShouldSuppress_DoesNotSuppressOtherDomains_WhenHostIsRealSmtp()
    {
        Assert.False(RecipientSuppression.ShouldSuppress("smtp.gmail.com", "test@example.com", SuppressedDomains));
    }

    [Theory]
    [InlineData("x@edu.gobeyond.ba")]
    [InlineData("x@EDU.GOBEYOND.BA")]
    [InlineData("x@a.b.gobeyond.ba")]
    public void ShouldSuppress_SuppressesSubdomainsOfConfiguredDomain_WhenHostIsRealSmtp(string recipient)
    {
        // NOT-08 / AUTH-03: qa.sec.2@edu.gobeyond.ba was really sent through Gmail because ShouldSuppress only
        // matched the domain exactly. edu.gobeyond.ba is a subdomain of the suppressed gobeyond.ba and belongs
        // to the same third party, so it must be suppressed too.
        Assert.True(RecipientSuppression.ShouldSuppress("smtp.gmail.com", recipient, SuppressedDomains));
    }

    [Fact]
    public void ShouldSuppress_DoesNotSuppressDomainThatMerelyEndsWithSuffix_WhenHostIsRealSmtp()
    {
        // "notgobeyond.ba" ends with the same letters as "gobeyond.ba" but is not a subdomain of it (no dot
        // boundary) and must NOT be suppressed - it's someone else's domain.
        Assert.False(RecipientSuppression.ShouldSuppress("smtp.gmail.com", "x@notgobeyond.ba", SuppressedDomains));
    }

    [Fact]
    public void ShouldSuppress_ReturnsFalse_WhenNoDomainsConfigured()
    {
        Assert.False(RecipientSuppression.ShouldSuppress("smtp.gmail.com", "mentor@gobeyond.ba", []));
    }

    [Fact]
    public void ShouldSuppress_ReturnsFalse_WhenSuppressedDomainsIsNull()
    {
        Assert.False(RecipientSuppression.ShouldSuppress("smtp.gmail.com", "mentor@gobeyond.ba", null));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("not-an-email")]
    public void ShouldSuppress_ReturnsFalse_ForRecipientWithoutDomain(string? recipient)
    {
        Assert.False(RecipientSuppression.ShouldSuppress("smtp.gmail.com", recipient, SuppressedDomains));
    }

    [Fact]
    public void ExtractDomain_ReturnsPartAfterLastAt()
    {
        Assert.Equal("gobeyond.ba", RecipientSuppression.ExtractDomain("mentor@gobeyond.ba"));
    }

    [Fact]
    public void IsMailpitHost_ReturnsFalse_ForRealSmtpHost()
    {
        Assert.False(RecipientSuppression.IsMailpitHost("smtp.gmail.com"));
    }
}
