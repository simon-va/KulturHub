using System.Security.Claims;

namespace KulturHub.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue("sub")
            ?? throw new InvalidOperationException("JWT claim 'sub' is missing.");
        return Guid.Parse(value);
    }
}
