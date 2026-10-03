namespace FreeCodeSpot.Attendance.Domain.Attendance;

/// <summary>
/// One stretch of work: when an employee clocked in and, once they have, when
/// they clocked out. A record without a clock-out time is open. Times are UTC.
/// </summary>
public record AttendanceRecord(int Id, int EmployeeId, DateTimeOffset ClockInUtc, DateTimeOffset? ClockOutUtc = null)
{
    public bool IsOpen => ClockOutUtc is null;
}
