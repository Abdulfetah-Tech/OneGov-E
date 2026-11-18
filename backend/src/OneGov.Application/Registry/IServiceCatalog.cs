using OneGov.Application.Common;

namespace OneGov.Application.Registry;

/// <summary>Read port for the service registry. Implemented in Infrastructure.</summary>
public interface IServiceCatalog
{
    Task<PagedResult<ServiceSummaryDto>> ListAsync(ListServicesQuery query, CancellationToken cancellationToken);

    Task<ServiceDetailDto?> GetByCodeAsync(string code, string language, CancellationToken cancellationToken);
}
