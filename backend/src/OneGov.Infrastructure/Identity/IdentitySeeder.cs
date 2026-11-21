using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OneGov.Domain.Identity;
using OneGov.Infrastructure.Persistence;

namespace OneGov.Infrastructure.Identity;

public static class IdentitySeeder
{
    /// <summary>Published on purpose: these accounts exist only when development seeding is on. Never use it elsewhere.</summary>
    public const string DevelopmentPassword = "Dev-Only-Password-1";

    /// <summary>Roles are required data, not sample data, so they are created on every start. Safe to repeat.</summary>
    public static async Task SeedRolesAsync(OneGovDbContext db, CancellationToken cancellationToken)
    {
        var existing = await db.Roles.Select(r => r.Name).ToListAsync(cancellationToken);
        var missing = Roles.All.Where(role => !existing.Contains(role)).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        foreach (var name in missing)
        {
            db.Roles.Add(new ApplicationRole
            {
                Name = name,
                NormalizedName = name.ToUpperInvariant(),
                ConcurrencyStamp = Guid.NewGuid().ToString(),
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>SAMPLE accounts for local development. Staff accounts have no second factor until they enrol one.</summary>
    public static async Task SeedDevelopmentUsersAsync(OneGovDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(u => u.Email == "citizen@example.test", cancellationToken))
        {
            return;
        }

        var roleIds = await db.Roles.Where(r => r.Name != null).ToDictionaryAsync(r => r.Name!, r => r.Id, cancellationToken);
        var agencyId = (await db.Agencies.FirstOrDefaultAsync(a => a.Code == "REV-SAMPLE", cancellationToken))?.Id;
        var hasher = new PasswordHasher<ApplicationUser>();

        void Add(string email, string displayName, string role, Guid? agency)
        {
            var user = new ApplicationUser
            {
                UserName = email,
                NormalizedUserName = email.ToUpperInvariant(),
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                EmailConfirmed = true,
                DisplayName = displayName,
                AgencyId = agency,
                LockoutEnabled = true,
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString(),
            };
            user.PasswordHash = hasher.HashPassword(user, DevelopmentPassword);
            db.Users.Add(user);
            db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = user.Id, RoleId = roleIds[role] });
        }

        Add("citizen@example.test", "Sample Citizen", Roles.Citizen, null);
        Add("officer@example.test", "Sample Officer", Roles.Officer, agencyId);
        Add("approver@example.test", "Sample Approver", Roles.Approver, agencyId);
        Add("admin@example.test", "Sample Platform Admin", Roles.PlatformAdmin, null);
        await db.SaveChangesAsync(cancellationToken);
    }
}
