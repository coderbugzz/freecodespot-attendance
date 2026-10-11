using FreeCodeSpot.Attendance.Application.Timesheets;

namespace FreeCodeSpot.Attendance.Application.Reports;

/// <summary>
/// Writes the report of a range, and the records behind it, to a workbook. Which
/// Excel library does the writing is the business of the class that implements this.
/// </summary>
public interface IReportWorkbook
{
    byte[] Create(AttendanceReport report, Timesheet records);
}
