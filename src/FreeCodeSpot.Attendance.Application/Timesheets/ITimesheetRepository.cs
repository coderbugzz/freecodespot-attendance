namespace FreeCodeSpot.Attendance.Application.Timesheets;

/// <summary>
/// Read-only queries for timesheets. They live apart from IAttendanceRepository,
/// which changes records, so the two can grow without getting in each other's way.
/// </summary>
public interface ITimesheetRepository
{
    /// <summary>
    /// Records that were clocked in from fromUtc up to, but not including, toUtc,
    /// for one employee or for everyone, ordered by employee name and clock-in time.
    /// </summary>
    Task<IReadOnlyList<TimesheetRecord>> GetRecordsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int? employeeId,
        CancellationToken cancellationToken = default);
}
