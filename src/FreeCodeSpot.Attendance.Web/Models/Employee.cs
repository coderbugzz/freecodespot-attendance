namespace FreeCodeSpot.Attendance.Web.Models;

/// <summary>
/// The shape of an employee as the API returns it.
/// </summary>
public record Employee(int Id, string FullName, string Department, string Email);
