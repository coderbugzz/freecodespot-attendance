using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace FreeCodeSpot.Attendance.Web.Security;

/// <summary>
/// Who signed in, what they may do, which employee they are, and the API token that proves it. The token
/// travels in a claim so it lives inside the encrypted sign-in cookie and never
/// reaches the browser in the clear.
/// </summary>
public sealed record SignedInUser(
    string Email,
    IReadOnlyList<string> Roles,
    int? EmployeeId,
    string AccessToken,
    TimeSpan ExpiresIn)
{
    public const string AccessTokenClaim = "access_token";
    public const string EmployeeIdClaim = "employee_id";

    public ClaimsPrincipal ToPrincipal()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, Email),
            new(AccessTokenClaim, AccessToken),
        };
        claims.AddRange(Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        // The employee this user is linked to. Pages use it to decide who gets My attendance.
        if (EmployeeId is { } employeeId)
        {
            claims.Add(new Claim(EmployeeIdClaim, employeeId.ToString()));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }
}
