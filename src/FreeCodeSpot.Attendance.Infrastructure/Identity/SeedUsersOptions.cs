namespace FreeCodeSpot.Attendance.Infrastructure.Identity;

/// <summary>
/// The demo accounts created on startup in development. The password is read
/// from configuration so it never appears in code. A roster email links the
/// account to the employee with that email; leave it empty for no link.
/// </summary>
public sealed class SeedUsersOptions
{
    public const string SectionName = "SeedUsers";

    public string ManagerEmail { get; set; } = "";
    public string EmployeeEmail { get; set; } = "";
    public string ManagerRosterEmail { get; set; } = "";
    public string EmployeeRosterEmail { get; set; } = "";
    public string Password { get; set; } = "";
}
