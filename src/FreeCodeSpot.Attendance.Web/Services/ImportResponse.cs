using FreeCodeSpot.Attendance.Web.Models;

namespace FreeCodeSpot.Attendance.Web.Services;

/// <summary>
/// What the API answered to an upload: the report of the rows, or the reason the file was
/// refused, for example a file that is not a workbook.
/// </summary>
public sealed record ImportResponse(EmployeeImportReport? Report, string? Message)
{
    public static ImportResponse Of(EmployeeImportReport report) => new(report, null);

    public static ImportResponse Refused(string message) => new(null, message);
}
