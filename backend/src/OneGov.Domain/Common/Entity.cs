namespace OneGov.Domain.Common;

/// <summary>Base type with a time-ordered GUID (UUIDv7) identifier.</summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();
}

/// <summary>
/// Entity with audit timestamps and soft delete. Timestamps are stamped by the persistence layer
/// from an injected <see cref="TimeProvider"/>, never read from the system clock in domain code.
/// </summary>
public abstract class AuditableEntity : Entity
{
    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public void SoftDelete(DateTimeOffset now)
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        DeletedAt = now;
    }
}
