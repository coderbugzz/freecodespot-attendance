namespace FreeCodeSpot.Attendance.Web.Security;

/// <summary>
/// Decides where to send a user after signing in. Only paths on this site are
/// followed, so the login page cannot be used to redirect someone elsewhere.
/// </summary>
public static class ReturnUrl
{
    public static string OrHome(string? url) => IsLocal(url) ? url! : "/";

    private static bool IsLocal(string? url) =>
        !string.IsNullOrEmpty(url)
        && url.StartsWith('/')
        && !url.StartsWith("//")
        && !url.StartsWith("/\\");
}
