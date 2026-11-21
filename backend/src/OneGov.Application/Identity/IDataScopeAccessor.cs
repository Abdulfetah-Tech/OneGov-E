using OneGov.Domain.Identity;

namespace OneGov.Application.Identity;

/// <summary>Supplies the data scope of whoever is making the current request. The data layer filters applications by it.</summary>
public interface IDataScopeAccessor
{
    DataScope Current { get; }
}
