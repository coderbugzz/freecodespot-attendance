namespace FreeCodeSpot.Attendance.Infrastructure.Identity;

/// <summary>
/// The demo accounts created on startup in development. The password is read
/// from configuration so it never appears in code.
/// </summary>
public sealed class SeedUsersOptions
{
    public const string SectionName = "SeedUsers";

    public string ManagerEmail { get; set; } = "";
    public string EmployeeEmail { get; set; } = "";
    public string Password { get; set; } = "";
}
