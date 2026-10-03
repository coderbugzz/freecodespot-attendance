namespace FreeCodeSpot.Attendance.Application.Timesheets;

/// <summary>
/// The attendance of one or more employees between two dates, both included.
/// The dates are days in the time zone named by TimeZoneId.
/// </summary>
public sealed record Timesheet(
    DateOnly From,
    DateOnly To,
    string TimeZoneId,
    IReadOnlyList<EmployeeTimesheet> Employees);

/// <summary>One employee's days in the range, and the time they worked in total.</summary>
public sealed record EmployeeTimesheet(
    int EmployeeId,
    string FullName,
    string Department,
    IReadOnlyList<TimesheetDay> Days,
    TimeSpan TotalWorked);

/// <summary>One day with its records. Worked adds up the closed records only.</summary>
public sealed record TimesheetDay(
    DateOnly Date,
    IReadOnlyList<TimesheetEntry> Entries,
    TimeSpan Worked);

/// <summary>One record. Duration is null while the employee is still clocked in.</summary>
public sealed record TimesheetEntry(
    int RecordId,
    DateTimeOffset ClockInUtc,
    DateTimeOffset? ClockOutUtc,
    TimeSpan? Duration);
