namespace FreeCodeSpot.Attendance.Application.Timesheets;

/// <summary>
/// One attendance record together with the employee it belongs to. The database
/// query projects straight into this, so the service never loads whole entities.
/// </summary>
public sealed record TimesheetRecord(
    int EmployeeId,
    string FullName,
    string Department,
    int RecordId,
    DateTimeOffset ClockInUtc,
    DateTimeOffset? ClockOutUtc);
