using FreeCodeSpot.Attendance.Web.Models;

namespace FreeCodeSpot.Attendance.Web.Services;

/// <summary>
/// What the API answered to a report request: the report, or the reason it was
/// refused, for example a range of dates that is too long.
/// </summary>
public sealed record ReportResponse(AttendanceReport? Report, string? Message)
{
    public static ReportResponse Of(AttendanceReport report) => new(report, null);

    public static ReportResponse Refused(string message) => new(null, message);
}
