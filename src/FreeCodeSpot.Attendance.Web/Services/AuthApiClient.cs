using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FreeCodeSpot.Attendance.Web.Security;

namespace FreeCodeSpot.Attendance.Web.Services;

/// <summary>
/// Signs a user in against the attendance API. The API owns the users and the
/// roles, so the Web app never sees a password hash.
/// </summary>
public sealed class AuthApiClient(HttpClient httpClient)
{
    /// <summary>Returns the signed-in user, or null when the email or password is wrong.</summary>
    public async Task<SignedInUser?> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        using var loginResponse = await httpClient.PostAsJsonAsync("auth/login", new { email, password }, cancellationToken);
        if (loginResponse.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        loginResponse.EnsureSuccessStatusCode();
        var token = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The API returned an empty login response.");

        // Ask the API who the token belongs to, so the roles come from the API and not from us.
        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "auth/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        using var meResponse = await httpClient.SendAsync(meRequest, cancellationToken);
        meResponse.EnsureSuccessStatusCode();
        var me = await meResponse.Content.ReadFromJsonAsync<MeResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The API returned an empty profile.");

        return new SignedInUser(me.Email, me.Roles, me.EmployeeId, token.AccessToken, TimeSpan.FromSeconds(token.ExpiresIn));
    }

    private sealed record TokenResponse(string AccessToken, long ExpiresIn);

    private sealed record MeResponse(string Email, string[] Roles, int? EmployeeId);
}
