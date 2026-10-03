using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace FreeCodeSpot.Attendance.Api.Auth;

/// <summary>
/// Maps the sign-in routes. Login checks the password and answers with a bearer
/// token. Me tells a signed-in caller who they are and which roles they hold.
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth")
            .WithTags("Authentication");

        group.MapPost("/login", async (LoginRequest request, SignInManager<IdentityUser> signInManager) =>
            {
                // Ask for a bearer token instead of a cookie.
                signInManager.AuthenticationScheme = IdentityConstants.BearerScheme;

                var result = await signInManager.PasswordSignInAsync(
                    request.Email, request.Password, isPersistent: false, lockoutOnFailure: true);

                // On success the bearer handler has already written the token to the response.
                return result.Succeeded
                    ? TypedResults.Empty
                    : Results.Problem("The email or password is not correct.", statusCode: StatusCodes.Status401Unauthorized);
            })
            .AllowAnonymous()
            .WithName("Login")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/me", (ClaimsPrincipal user) =>
                Results.Ok(new CurrentUser(
                    user.FindFirstValue(ClaimTypes.Email) ?? user.Identity?.Name ?? "",
                    user.FindAll(ClaimTypes.Role).Select(claim => claim.Value).Order().ToList())))
            .RequireAuthorization()
            .WithName("GetCurrentUser")
            .Produces<CurrentUser>()
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}

/// <summary>The body of a login request.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Who the bearer token belongs to.</summary>
public sealed record CurrentUser(string Email, IReadOnlyList<string> Roles);
