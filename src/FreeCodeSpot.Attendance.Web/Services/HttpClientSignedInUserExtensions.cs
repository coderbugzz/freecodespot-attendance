using System.Net.Http.Headers;
using FreeCodeSpot.Attendance.Web.Security;
using Microsoft.AspNetCore.Components.Authorization;

namespace FreeCodeSpot.Attendance.Web.Services;

public static class HttpClientSignedInUserExtensions
{
    /// <summary>
    /// Puts the API token of the signed-in user in the Authorization header, or
    /// clears the header when nobody is signed in. The API clients call this
    /// before every request.
    /// </summary>
    public static async Task UseSignedInUserAsync(
        this HttpClient httpClient,
        AuthenticationStateProvider authenticationStateProvider)
    {
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        var token = state.User.FindFirst(SignedInUser.AccessTokenClaim)?.Value;

        httpClient.DefaultRequestHeaders.Authorization =
            token is null ? null : new AuthenticationHeaderValue("Bearer", token);
    }
}
