namespace FreeCodeSpot.Attendance.Domain.Employees;

/// <summary>
/// A person whose attendance the system tracks.
/// </summary>
public record Employee(int Id, string FullName, string Department, string Email);
