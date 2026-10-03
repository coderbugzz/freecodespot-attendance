namespace FreeCodeSpot.Attendance.Web.Services;

public static class DurationFormat
{
    /// <summary>Shows a duration the way people say it, for example 7h 30m.</summary>
    public static string ToHoursAndMinutes(this TimeSpan duration) =>
        $"{(int)duration.TotalHours}h {duration.Minutes:00}m";
}
