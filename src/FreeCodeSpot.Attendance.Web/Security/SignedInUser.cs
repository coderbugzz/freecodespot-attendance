using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace FreeCodeSpot.Attendance.Web.Security;

/// <summary>
/// Who signed in, what they may do, and the API token that proves it. The token
/// travels in a claim so it lives inside the encrypted sign-in cookie and never
/// reaches the browser in the clear.
/// </summary>
public sealed record SignedInUser(string Email, IReadOnlyList<string> Roles, string AccessToken, TimeSpan ExpiresIn)
{
    public const string AccessTokenClaim = "access_token";

    public ClaimsPrincipal ToPrincipal()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, Email),
            new(AccessTokenClaim, AccessToken),
        };
        claims.AddRange(Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }
}
