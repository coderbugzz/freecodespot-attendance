using FreeCodeSpot.Attendance.Domain.Attendance;

namespace FreeCodeSpot.Attendance.Application.Attendance;

/// <summary>
/// Storage for attendance records. The Application layer owns this contract,
/// like it does for the roster.
/// </summary>
public interface IAttendanceRepository
{
    /// <summary>Every record that has no clock-out time yet.</summary>
    Task<IReadOnlyList<AttendanceRecord>> GetOpenRecordsAsync(CancellationToken cancellationToken = default);

    Task<AttendanceRecord?> GetOpenRecordAsync(int employeeId, CancellationToken cancellationToken = default);

    /// <summary>The latest records of one employee, newest first, open or closed.</summary>
    Task<IReadOnlyList<AttendanceRecord>> GetRecentRecordsAsync(int employeeId, int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a new record. Returns null when the employee already has an open
    /// record, which the database refuses even if two requests arrive together.
    /// </summary>
    Task<AttendanceRecord?> TryAddAsync(AttendanceRecord record, CancellationToken cancellationToken = default);

    Task UpdateAsync(AttendanceRecord record, CancellationToken cancellationToken = default);
}
