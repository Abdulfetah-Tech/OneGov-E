using Microsoft.AspNetCore.Identity;

namespace OneGov.Infrastructure.Identity;

/// <summary>A local account. National-ID (Fayda) sign-in will link to this record in a later slice.</summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        Id = Guid.CreateVersion7();
    }

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>One of en, am, om.</summary>
    public string PreferredLanguage { get; set; } = "en";

    /// <summary>Staff accounts belong to one agency. Null for citizens and platform administrators.</summary>
    public Guid? AgencyId { get; set; }

    /// <summary>Administrators disable an account here. Disabled accounts cannot sign in or refresh.</summary>
    public bool IsActive { get; set; } = true;
}

public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole()
    {
        Id = Guid.CreateVersion7();
    }
}
