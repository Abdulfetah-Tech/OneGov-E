using OneGov.Domain.Common;

namespace OneGov.Domain.Registry;

public sealed class Agency : AuditableEntity
{
    private Agency()
    {
    }

    public Agency(string code, LocalizedText name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code.Trim().ToUpperInvariant();
        Name = name ?? throw new ArgumentNullException(nameof(name));
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public LocalizedText Name { get; private set; } = null!;

    public bool IsActive { get; private set; }
}
