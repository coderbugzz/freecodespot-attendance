namespace FreeCodeSpot.Attendance.Application.Attendance;

/// <summary>
/// An employee and whether they are clocked in right now. ClockedInSinceUtc is
/// null when they are not.
/// </summary>
public sealed record EmployeeAttendanceStatus(
    int EmployeeId,
    string FullName,
    string Department,
    DateTimeOffset? ClockedInSinceUtc);
