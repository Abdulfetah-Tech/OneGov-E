using OneGov.Api.Http;
using OneGov.Application.Identity;
using OneGov.Domain.Identity;

namespace OneGov.Api.Security;

/// <summary>Builds the data scope from the signed-in user's access token. Anything unauthenticated gets no access.</summary>
public sealed class HttpDataScopeAccessor(IHttpContextAccessor httpContextAccessor) : IDataScopeAccessor
{
    public DataScope Current
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return DataScope.None;
            }

            Guid? agencyId = Guid.TryParse(user.FindFirst("agency")?.Value, out var agency) ? agency : null;
            return DataScope.For(
                user.GetUserId(),
                agencyId,
                user.FindAll("role").Select(claim => claim.Value),
                user.HasClaim("amr", "otp"));
        }
    }
}
