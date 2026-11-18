namespace OneGov.Application.Registry;

/// <summary>Query for the public service directory. <c>Language</c> is one of en, am, om.</summary>
public sealed record ListServicesQuery(
    string? Category = null,
    string? Search = null,
    string Language = "en",
    int Page = 1,
    int PageSize = 20);

public sealed record ServiceSummaryDto(
    Guid Id,
    string Code,
    string Name,
    string Description,
    string Category,
    string Status,
    string AgencyCode,
    string AgencyName);

public sealed record RequirementDto(string Kind, string Text, int Order);

public sealed record ServiceDetailDto(
    Guid Id,
    string Code,
    string Name,
    string Description,
    string Category,
    string Status,
    string AgencyCode,
    string AgencyName,
    IReadOnlyList<RequirementDto> Requirements);
