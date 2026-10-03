namespace FreeCodeSpot.Attendance.Web.Services;

/// <summary>
/// What happened when the Web app asked the API to clock an employee in or out.
/// Message holds the reason when the API refused.
/// </summary>
public sealed record ClockResult(bool Succeeded, string? Message)
{
    public static ClockResult Done { get; } = new(true, null);

    public static ClockResult Refused(string message) => new(false, message);
}
