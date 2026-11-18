using Microsoft.EntityFrameworkCore;
using OneGov.Application.Common;
using OneGov.Application.Registry;
using OneGov.Domain.Registry;
using OneGov.Infrastructure.Persistence;

namespace OneGov.Infrastructure.Services;

public sealed class ServiceCatalog(OneGovDbContext db) : IServiceCatalog
{
    public async Task<PagedResult<ServiceSummaryDto>> ListAsync(ListServicesQuery query, CancellationToken cancellationToken)
    {
        var services = db.Services.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Category)
            && Enum.TryParse<ServiceCategory>(query.Category, ignoreCase: true, out var category))
        {
            services = services.Where(s => s.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{EscapeLike(query.Search.Trim())}%";
            services = services.Where(s =>
                EF.Functions.ILike(s.Name.En, pattern)
                || EF.Functions.ILike(s.Name.Am, pattern)
                || EF.Functions.ILike(s.Name.Om, pattern)
                || EF.Functions.ILike(s.Code, pattern));
        }

        var total = await services.CountAsync(cancellationToken);
        var rows = await services
            .OrderBy(s => s.Status == ServiceStatus.Available ? 0 : 1)
            .ThenBy(s => s.Name.En)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(s => new
            {
                s.Id,
                s.Code,
                NameEn = s.Name.En,
                NameAm = s.Name.Am,
                NameOm = s.Name.Om,
                DescriptionEn = s.Description.En,
                DescriptionAm = s.Description.Am,
                DescriptionOm = s.Description.Om,
                s.Category,
                s.Status,
                AgencyCode = s.Agency!.Code,
                AgencyNameEn = s.Agency!.Name.En,
                AgencyNameAm = s.Agency!.Name.Am,
                AgencyNameOm = s.Agency!.Name.Om,
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new ServiceSummaryDto(
                r.Id,
                r.Code,
                new LocalizedText(r.NameEn, r.NameAm, r.NameOm).Get(query.Language),
                new LocalizedText(r.DescriptionEn, r.DescriptionAm, r.DescriptionOm).Get(query.Language),
                r.Category.ToString(),
                r.Status.ToString(),
                r.AgencyCode,
                new LocalizedText(r.AgencyNameEn, r.AgencyNameAm, r.AgencyNameOm).Get(query.Language)))
            .ToList();

        return new PagedResult<ServiceSummaryDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<ServiceDetailDto?> GetByCodeAsync(string code, string language, CancellationToken cancellationToken)
    {
        var normalized = code.Trim().ToUpperInvariant();
        var row = await db.Services
            .AsNoTracking()
            .Where(s => s.Code == normalized)
            .Select(s => new
            {
                s.Id,
                s.Code,
                NameEn = s.Name.En,
                NameAm = s.Name.Am,
                NameOm = s.Name.Om,
                DescriptionEn = s.Description.En,
                DescriptionAm = s.Description.Am,
                DescriptionOm = s.Description.Om,
                s.Category,
                s.Status,
                AgencyCode = s.Agency!.Code,
                AgencyNameEn = s.Agency!.Name.En,
                AgencyNameAm = s.Agency!.Name.Am,
                AgencyNameOm = s.Agency!.Name.Om,
                Requirements = s.Requirements
                    .OrderBy(r => r.Order)
                    .Select(r => new { r.Kind, TextEn = r.Text.En, TextAm = r.Text.Am, TextOm = r.Text.Om, r.Order })
                    .ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        return new ServiceDetailDto(
            row.Id,
            row.Code,
            new LocalizedText(row.NameEn, row.NameAm, row.NameOm).Get(language),
            new LocalizedText(row.DescriptionEn, row.DescriptionAm, row.DescriptionOm).Get(language),
            row.Category.ToString(),
            row.Status.ToString(),
            row.AgencyCode,
            new LocalizedText(row.AgencyNameEn, row.AgencyNameAm, row.AgencyNameOm).Get(language),
            row.Requirements
                .Select(r => new RequirementDto(r.Kind.ToString(), new LocalizedText(r.TextEn, r.TextAm, r.TextOm).Get(language), r.Order))
                .ToList());
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
