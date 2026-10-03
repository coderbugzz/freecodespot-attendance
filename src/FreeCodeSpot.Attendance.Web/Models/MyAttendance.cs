namespace FreeCodeSpot.Attendance.Web.Models;

/// <summary>
/// The signed-in user's own attendance as the API returns it: the status, and
/// the latest records, newest first.
/// </summary>
public record MyAttendance(EmployeeAttendanceStatus Status, IReadOnlyList<AttendanceEntry> RecentRecords);

/// <summary>One clock-in, and the clock-out that closed it. ClockOutUtc is null while it is open.</summary>
public record AttendanceEntry(int Id, DateTimeOffset ClockInUtc, DateTimeOffset? ClockOutUtc);
