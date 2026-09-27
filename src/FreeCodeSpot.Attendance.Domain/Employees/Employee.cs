namespace FreeCodeSpot.Attendance.Domain.Employees;

/// <summary>
/// A person whose attendance the system tracks. Employees are never deleted,
/// because their attendance history has to stay. They are deactivated instead.
/// </summary>
public record Employee(int Id, string FullName, string Department, string Email, bool IsActive = true);
