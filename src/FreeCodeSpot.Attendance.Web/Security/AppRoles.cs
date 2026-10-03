namespace FreeCodeSpot.Attendance.Web.Security;

/// <summary>
/// The role names the pages check. They match the names the API puts in its
/// tokens. The Web app keeps its own copy, like it does for the employee model,
/// so it does not reference the API's projects.
/// </summary>
public static class AppRoles
{
    public const string Manager = "Manager";
    public const string Employee = "Employee";
}
