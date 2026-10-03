namespace FreeCodeSpot.Attendance.Web.Models;

/// <summary>
/// A timesheet as the API returns it. The days are days in the time zone named by
/// TimeZoneId, so the page shows the times in that zone too.
/// </summary>
public record Timesheet(DateOnly From, DateOnly To, string TimeZoneId, IReadOnlyList<EmployeeTimesheet> Employees);

public record EmployeeTimesheet(
    int EmployeeId,
    string FullName,
    string Department,
    IReadOnlyList<TimesheetDay> Days,
    TimeSpan TotalWorked);

public record TimesheetDay(DateOnly Date, IReadOnlyList<TimesheetEntry> Entries, TimeSpan Worked);

/// <summary>Duration is null while the employee is still clocked in.</summary>
public record TimesheetEntry(int RecordId, DateTimeOffset ClockInUtc, DateTimeOffset? ClockOutUtc, TimeSpan? Duration);
