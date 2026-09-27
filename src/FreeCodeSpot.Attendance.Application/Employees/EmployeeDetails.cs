namespace FreeCodeSpot.Attendance.Application.Employees;

/// <summary>
/// What a caller sends to create or edit an employee. The values come from a
/// request body, so any of them can be null even though the types say otherwise.
/// </summary>
public sealed record EmployeeDetails(string FullName, string Department, string Email);
