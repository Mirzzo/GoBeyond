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
        // A subdomain belongs to the same third party as the configured domain.
        Assert.True(RecipientSuppression.ShouldSuppress("smtp.gmail.com", recipient, SuppressedDomains));
    }

    [Fact]
    public void ShouldSuppress_DoesNotSuppressDomainThatMerelyEndsWithSuffix_WhenHostIsRealSmtp()
    {
        // "notgobeyond.ba" ends with the same letters as "gobeyond.ba" but is not a subdomain of it (no dot
        // boundary) and must NOT be suppressed - it's someone else's domain.
        Assert.False(RecipientSuppression.ShouldSuppress("smtp.gmail.com", "x@notgobeyond.ba", SuppressedDomains));
    }

    // MailAddress (used by SmtpEmailSender) normalizes these forms to the plain protected address.
    [Theory]
    [InlineData("x@gobeyond.ba.")] // trailing dot on the exact base domain
    [InlineData("x@edu.gobeyond.ba.")] // trailing dot on a subdomain
    [InlineData("<x@gobeyond.ba>")] // angle-address form, base domain
    [InlineData("<x@edu.gobeyond.ba>")] // angle-address form, subdomain
    [InlineData("x@edu.gobeyond.ba(note)")] // RFC 5322 comment form
    public void ShouldSuppress_SuppressesAddressFormsThatMailAddressNormalizesToAProtectedDomain_WhenHostIsRealSmtp(string recipient)
    {
        Assert.True(RecipientSuppression.ShouldSuppress("smtp.gmail.com", recipient, SuppressedDomains));
    }

    // SmtpClient sends a non-ASCII host in its IdnMapping.GetAscii form, where these become the protected domain.
    [Theory]
    [InlineData("x@ｇobeyond.ba")] // fullwidth 'g' (U+FF47) folds to ASCII 'g'
    [InlineData("x@gobeyond.ba​")] // trailing zero-width space (U+200B) is dropped
    [InlineData("x@edu.gobeyond。ba")] // ideographic full stop (U+3002) is an IDNA label separator, same as '.'
    [InlineData("x@edu.gobeyond．ba")] // fullwidth full stop (U+FF0E), same as above
    public void ShouldSuppress_SuppressesIdnNormalizedFormsOfAProtectedDomain_WhenHostIsRealSmtp(string recipient)
    {
        Assert.True(RecipientSuppression.ShouldSuppress("smtp.gmail.com", recipient, SuppressedDomains));
    }

    // IdnMapping turns each of these endings into a trailing ASCII '.', so SmtpClient sends RCPT TO:<x@gobeyond.ba.>.
    [Theory]
    [InlineData("x@gobeyond.ba。")] // trailing ideographic full stop (U+3002)
    [InlineData("x@edu.gobeyond.ba．")] // trailing fullwidth full stop (U+FF0E), subdomain
    [InlineData("x@gobeyond.ba｡")] // trailing halfwidth ideographic full stop (U+FF61)
    [InlineData("x@gobeyond.ba.​")] // trailing '.' followed by a zero-width space (U+200B)
    public void ShouldSuppress_SuppressesATrailingUnicodeFullStop_WhenHostIsRealSmtp(string recipient)
    {
        Assert.True(RecipientSuppression.ShouldSuppress("smtp.gmail.com", recipient, SuppressedDomains));
    }

    [Fact]
    public void ExtractDomain_ConvertsFullwidthLetterToAscii()
    {
        Assert.Equal("gobeyond.ba", RecipientSuppression.ExtractDomain("x@ｇobeyond.ba"));
    }

    [Fact]
    public void ExtractDomain_ConvertsIdeographicFullStopToAsciiDot()
    {
        Assert.Equal("edu.gobeyond.ba", RecipientSuppression.ExtractDomain("x@edu.gobeyond。ba"));
    }

    [Fact]
    public void ExtractDomain_TrimsTrailingDot()
    {
        Assert.Equal("gobeyond.ba", RecipientSuppression.ExtractDomain("x@gobeyond.ba."));
    }

    [Fact]
    public void ExtractDomain_TrimsATrailingIdeographicFullStop()
    {
        Assert.Equal("gobeyond.ba", RecipientSuppression.ExtractDomain("x@gobeyond.ba。"));
    }

    [Fact]
    public void ExtractDomain_TrimsATrailingDotFollowedByAZeroWidthSpace()
    {
        Assert.Equal("gobeyond.ba", RecipientSuppression.ExtractDomain("x@gobeyond.ba.​"));
    }

    [Theory]
    [InlineData("x@školica.ba")]
    [InlineData("x@edu.školica.ba")]
    public void ShouldSuppress_ComparesAConfiguredUnicodeDomainInItsAsciiForm(string recipient)
    {
        string[] configured = ["školica.ba"];
        var punycodeRecipient = "x@" + new System.Globalization.IdnMapping().GetAscii(recipient[2..]);

        Assert.True(RecipientSuppression.ShouldSuppress("smtp.gmail.com", recipient, configured));
        Assert.True(RecipientSuppression.ShouldSuppress("smtp.gmail.com", punycodeRecipient, configured));
    }

    [Fact]
    public void ExtractDomain_ParsesAngleAddressForm()
    {
        Assert.Equal("edu.gobeyond.ba", RecipientSuppression.ExtractDomain("<x@edu.gobeyond.ba>"));
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
