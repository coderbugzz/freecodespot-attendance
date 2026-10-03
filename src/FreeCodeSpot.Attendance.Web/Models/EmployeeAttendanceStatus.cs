namespace FreeCodeSpot.Attendance.Web.Models;

/// <summary>
/// An employee and whether they are clocked in, as the API returns it.
/// ClockedInSinceUtc is null when the employee is not clocked in.
/// </summary>
public record EmployeeAttendanceStatus(int EmployeeId, string FullName, string Department, DateTimeOffset? ClockedInSinceUtc);
