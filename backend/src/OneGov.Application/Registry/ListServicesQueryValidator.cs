using FluentValidation;
using OneGov.Domain.Registry;

namespace OneGov.Application.Registry;

public sealed class ListServicesQueryValidator : AbstractValidator<ListServicesQuery>
{
    public static readonly IReadOnlySet<string> SupportedLanguages = new HashSet<string> { "en", "am", "om" };

    public ListServicesQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 50);
        RuleFor(q => q.Language)
            .Must(language => SupportedLanguages.Contains(language))
            .WithMessage("Language must be one of: en, am, om.");
        RuleFor(q => q.Search).MaximumLength(100);
        RuleFor(q => q.Category)
            .Must(category => string.IsNullOrWhiteSpace(category) || Enum.TryParse<ServiceCategory>(category, ignoreCase: true, out _))
            .WithMessage("Unknown service category.");
    }
}
