using System.ComponentModel.DataAnnotations;

namespace QCPCMvc.Validation;

/// <summary>Requires the email to end with one of the allowed domains.</summary>
[AttributeUsage(AttributeTargets.Property)]
public class AllowedEmailDomainAttribute : ValidationAttribute
{
    private readonly string[] _domains;

    public AllowedEmailDomainAttribute(params string[] domains)
    {
        _domains = domains;
        ErrorMessage = $"Only {string.Join(" or ", domains)} email addresses are allowed.";
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext ctx)
    {
        if (value is not string email) return ValidationResult.Success;
        var lower = email.ToLowerInvariant();
        if (_domains.Any(d => lower.EndsWith("@" + d.TrimStart('@').ToLowerInvariant())))
            return ValidationResult.Success;

        return new ValidationResult(ErrorMessage);
    }
}
