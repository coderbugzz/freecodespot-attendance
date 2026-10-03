using FreeCodeSpot.Attendance.Domain.Attendance;

namespace FreeCodeSpot.Attendance.Application.Attendance;

/// <summary>
/// What an employee sees about themselves: whether they are clocked in, and
/// their latest records, newest first.
/// </summary>
public sealed record MyAttendance(
    EmployeeAttendanceStatus Status,
    IReadOnlyList<AttendanceRecord> RecentRecords);
