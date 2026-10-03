using FreeCodeSpot.Attendance.Web.Models;

namespace FreeCodeSpot.Attendance.Web.Services;

/// <summary>
/// What the API answered to a timesheet request: the timesheet, or the reason it
/// was refused, for example a range of dates that is too long.
/// </summary>
public sealed record TimesheetResponse(Timesheet? Timesheet, string? Message)
{
    public static TimesheetResponse Of(Timesheet timesheet) => new(timesheet, null);

    public static TimesheetResponse Refused(string message) => new(null, message);
}
