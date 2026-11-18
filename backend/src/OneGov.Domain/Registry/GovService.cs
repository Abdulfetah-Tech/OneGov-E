using OneGov.Domain.Common;

namespace OneGov.Domain.Registry;

public enum ServiceCategory
{
    IdentityAndCivilRegistration,
    RevenueAndTax,
    Immigration,
    Education,
    Healthcare,
    BusinessRegistration,
    Transportation,
    LandAndProperty,
    LocalGovernment,
}

/// <summary>Only <see cref="Available"/> services have an implemented workflow.</summary>
public enum ServiceStatus
{
    Planned,
    Available,
    Suspended,
}

public enum RequirementKind
{
    Eligibility,
    Document,
    Step,
}

public sealed class ServiceRequirement : Entity
{
    private ServiceRequirement()
    {
    }

    internal ServiceRequirement(Guid serviceId, RequirementKind kind, LocalizedText text, int order)
    {
        ServiceId = serviceId;
        Kind = kind;
        Text = text;
        Order = order;
    }

    public Guid ServiceId { get; private set; }

    public RequirementKind Kind { get; private set; }

    public LocalizedText Text { get; private set; } = null!;

    public int Order { get; private set; }
}

public sealed class GovService : AuditableEntity
{
    private readonly List<ServiceRequirement> _requirements = [];

    private GovService()
    {
    }

    public GovService(
        string code,
        Guid agencyId,
        ServiceCategory category,
        LocalizedText name,
        LocalizedText description,
        ServiceStatus status,
        string? workflowCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        if (status == ServiceStatus.Available && string.IsNullOrWhiteSpace(workflowCode))
        {
            throw new ArgumentException("An available service must reference a workflow.", nameof(workflowCode));
        }

        Code = code.Trim().ToUpperInvariant();
        AgencyId = agencyId;
        Category = category;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        Status = status;
        WorkflowCode = workflowCode;
    }

    public string Code { get; private set; } = string.Empty;

    public Guid AgencyId { get; private set; }

    public Agency? Agency { get; private set; }

    public ServiceCategory Category { get; private set; }

    public LocalizedText Name { get; private set; } = null!;

    public LocalizedText Description { get; private set; } = null!;

    public ServiceStatus Status { get; private set; }

    public string? WorkflowCode { get; private set; }

    public IReadOnlyList<ServiceRequirement> Requirements => _requirements;

    public void AddRequirement(RequirementKind kind, LocalizedText text, int order) =>
        _requirements.Add(new ServiceRequirement(Id, kind, text, order));
}
