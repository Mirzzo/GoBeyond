using System.ComponentModel.DataAnnotations;
using GoBeyond.API.Validation;
using GoBeyond.Core.DTOs.Auth;
using GoBeyond.Core.Validation;
using GoBeyond.Infrastructure.Services.Activity;

namespace GoBeyond.Tests.Validation;

public class ValidationRulesTests
{
    [Theory]
    [InlineData("lozinka123")]
    [InlineData("Abcdefg1")]
    [InlineData("12345678a")]
    [InlineData("Šifra2026!")]
    public void PasswordRules_AcceptValidPasswords(string password)
    {
        Assert.True(PasswordRules.IsValid(password));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("test")]            // prekratka (seed lozinka ne prolazi kroz registraciju)
    [InlineData("abc12")]           // prekratka
    [InlineData("samoslova")]       // bez broja
    [InlineData("1234567890")]      // bez slova
    public void PasswordRules_RejectInvalidPasswords(string? password)
    {
        Assert.False(PasswordRules.IsValid(password));
    }

    [Fact]
    public void PasswordRules_RejectPasswordLongerThan64Characters()
    {
        Assert.False(PasswordRules.IsValid(new string('a', 64) + "1"));
        Assert.True(PasswordRules.IsValid(new string('a', 63) + "1"));
    }

    [Theory]
    [InlineData("+387 61 123 456", true)]
    [InlineData("+38761123456", true)]
    [InlineData("+387 62 123 4567", true)]
    [InlineData("061 123 456", false)]
    [InlineData("+385 91 123 456", false)]
    [InlineData("abc", false)]
    public void PhonePattern_RequiresBosnianMobileFormat(string phone, bool valid)
    {
        Assert.Equal(valid, System.Text.RegularExpressions.Regex.IsMatch(phone, ValidationPatterns.Phone));
    }

    [Theory]
    [InlineData("ime@domena.com", true)]
    [InlineData("mirza.r@gobeyond.ba", true)]
    [InlineData("ime@domena", false)]
    [InlineData("imedomena.com", false)]
    public void EmailPattern_RequiresDomainWithTld(string email, bool valid)
    {
        Assert.Equal(valid, System.Text.RegularExpressions.Regex.IsMatch(email, ValidationPatterns.Email));
    }

    [Fact]
    public void RegisterClientRequest_WithInvalidFields_ReturnsBosnianMessagesPerField()
    {
        var request = new RegisterClientRequest
        {
            FirstName = "A",
            LastName = "Hadžić",
            Username = "t",
            Email = "not-an-email",
            PhoneNumber = "123",
            DateOfBirth = new DateOnly(2020, 1, 1),
            GenderId = 0,
            Password = "kratka",
            ConfirmPassword = "druga",
            WeightKg = 10,
            HeightCm = 180,
            FitnessLevelId = 1,
            TrainingExperienceYears = 2,
            FitnessGoalId = 1
        };

        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        var byField = results.SelectMany(r => r.MemberNames.Select(m => (m, r.ErrorMessage))).ToLookup(x => x.m, x => x.ErrorMessage);

        Assert.Contains(ValidationPatterns.EmailMessage, byField["Email"]);
        Assert.Contains(ValidationPatterns.PhoneMessage, byField["PhoneNumber"]);
        Assert.Contains(PasswordRules.Message, byField["Password"]);
        Assert.Contains("Lozinke se ne podudaraju.", byField["ConfirmPassword"]);
        Assert.Contains("Odaberite spol.", byField["GenderId"]);
        Assert.Contains("Težina mora biti između 30 i 300 kg.", byField["WeightKg"]);
        Assert.NotEmpty(byField["DateOfBirth"]);
        Assert.NotEmpty(byField["Username"]);
        Assert.NotEmpty(byField["FirstName"]);
    }

    [Theory]
    [InlineData("Email", "email")]
    [InlineData("$.genderId", "genderId")]
    [InlineData("Mentor.Bio", "mentor.bio")]
    [InlineData("Questionnaire.PrimaryGoal", "questionnaire.primaryGoal")]
    [InlineData("", "body")]
    public void ValidationResponseFactory_UsesCamelCaseFieldKeys(string key, string expected)
    {
        Assert.Equal(expected, ValidationResponseFactory.ToCamelCasePath(key));
    }

    [Theory]
    [InlineData(60, 90, 60)]   // redovan heartbeat svakih 60 s
    [InlineData(95, 90, 90)]   // zakašnjeli poziv: najviše 90 s
    [InlineData(600, 90, 0)]   // duga pauza = nova sesija
    [InlineData(-5, 90, 0)]
    public void Heartbeat_CreditsAtMostConfiguredSecondsPerCall(double elapsed, int max, int expected)
    {
        Assert.Equal(expected, ActivityService.CreditSeconds(elapsed, max));
    }
}
