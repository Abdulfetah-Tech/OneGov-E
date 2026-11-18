using FluentValidation;
using OneGov.Application.Common;
using OneGov.Domain.Common;

namespace OneGov.Application.Registry;

/// <summary>Use case: browse and read the public service directory.</summary>
public sealed class ServiceDirectoryService(IServiceCatalog catalog, IValidator<ListServicesQuery> validator)
{
    public async Task<Result<PagedResult<ServiceSummaryDto>>> ListAsync(ListServicesQuery query, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<PagedResult<ServiceSummaryDto>>.Failure(
                "registry.invalid_query", string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        return Result<PagedResult<ServiceSummaryDto>>.Success(await catalog.ListAsync(query, cancellationToken));
    }

    public async Task<Result<ServiceDetailDto>> GetAsync(string code, string language, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 64)
        {
            return Result<ServiceDetailDto>.Failure("registry.invalid_code", "Service code is invalid.");
        }

        if (!ListServicesQueryValidator.SupportedLanguages.Contains(language))
        {
            return Result<ServiceDetailDto>.Failure("registry.invalid_query", "Language must be one of: en, am, om.");
        }

        var service = await catalog.GetByCodeAsync(code, language, cancellationToken);
        return service is null
            ? Result<ServiceDetailDto>.Failure("registry.not_found", "Service not found.")
            : Result<ServiceDetailDto>.Success(service);
    }
}
