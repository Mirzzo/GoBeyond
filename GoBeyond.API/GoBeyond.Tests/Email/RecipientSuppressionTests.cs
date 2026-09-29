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

    // IdnMapping maps look-alikes of '@' (U+FF20, U+FE6B) and '>' (U+FF1E, U+FE65) to ASCII, so SmtpClient would send
    // e.g. RCPT TO:<x@edu@gobeyond.ba> or <x@gobeyond.ba>>, which a lenient server delivers to the protected domain. A
    // comma-separated list is split into several recipients by MailMessage.To.Add.
    [Theory]
    [InlineData("x@gobeyond.ba\uFE65")]
    [InlineData("x@gobeyond.ba.\uFE65")]
    [InlineData("x@edu.gobeyond.ba\uFE65")]
    [InlineData("<x@gobeyond.ba\uFE65>")]
    [InlineData("x@\uFE6Bgobeyond.ba")]
    [InlineData("x@edu\uFE6Bgobeyond.ba")]
    [InlineData("x@gobeyond.ba\uFF1E")]
    [InlineData("x@gobeyond.ba.\uFF1E")]
    [InlineData("x@edu.gobeyond.ba\uFF1E")]
    [InlineData("<x@gobeyond.ba\uFF1E>")]
    [InlineData("x@\uFF20gobeyond.ba")]
    [InlineData("x@edu\uFF20gobeyond.ba")]
    [InlineData("x@gobeyond.ba,a@example.org")]
    [InlineData("x@gobeyond.ba, a@example.org")]
    public void Evaluate_SuppressesAProtectedDomainInsideAHostThatIsNotAValidDnsName(string recipient)
    {
        Assert.Equal(SuppressionReason.ProtectedDomain, RecipientSuppression.Evaluate("smtp.gmail.com", recipient, SuppressedDomains));
        Assert.True(RecipientSuppression.ShouldSuppress("smtp.gmail.com", recipient, SuppressedDomains));
    }

    // Not a protected domain, but still not a DNS host name after IDN mapping: never sent through a real SMTP host,
    // also when no domains are configured.
    [Theory]
    [InlineData("x@go\uFF20beyond.ba")] // go@beyond.ba
    [InlineData("x@example\uFE65.org")] // example>.org
    [InlineData("x@example.org\u00A0")] // no-break space becomes a trailing ASCII space
    [InlineData("x@exa\u3000mple.org")] // ideographic space becomes an ASCII space
    public void Evaluate_SuppressesAHostThatIsNotAValidDnsName_EvenWithoutConfiguredDomains(string recipient)
    {
        Assert.Equal(SuppressionReason.InvalidHostName, RecipientSuppression.Evaluate("smtp.gmail.com", recipient, SuppressedDomains));
        Assert.Equal(SuppressionReason.InvalidHostName, RecipientSuppression.Evaluate("smtp.gmail.com", recipient, []));
        Assert.Equal(SuppressionReason.None, RecipientSuppression.Evaluate("mailpit", recipient, SuppressedDomains));
    }

    [Theory]
    [InlineData("x@example.org")]
    [InlineData("x@\uFF45xample.org")] // fullwidth 'e' maps to the valid host example.org
    [InlineData("x@m\u00FCnchen.de")] // sent as xn--mnchen-3ya.de
    [InlineData("Ime Prezime <x@sub-domena.example.org>")]
    public void Evaluate_SendsToAValidHostThatIsNotProtected(string recipient)
    {
        Assert.Equal(SuppressionReason.None, RecipientSuppression.Evaluate("smtp.gmail.com", recipient, SuppressedDomains));
    }

    [Fact]
    public void ExtractDomains_ReturnsTheHostOfEveryRecipientInAList()
    {
        Assert.Equal(["gobeyond.ba", "example.org"], RecipientSuppression.ExtractDomains("x@gobeyond.ba, a@example.org"));
    }

    [Fact]
    public void ExtractDomain_KeepsTheAsciiFormOfALookAlikeAtSign()
    {
        Assert.Equal("edu@gobeyond.ba", RecipientSuppression.ExtractDomain("x@edu\uFF20gobeyond.ba"));
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
