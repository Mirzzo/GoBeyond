using System.ComponentModel.DataAnnotations;

namespace GoBeyond.Core.Validation;

/// <summary>Lozinka po pravilima iz <see cref="PasswordRules"/>.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class PasswordAttribute : ValidationAttribute
{
    public PasswordAttribute() : base(PasswordRules.Message) { }

    public override bool IsValid(object? value) => value is null || PasswordRules.IsValid(value as string);
}
