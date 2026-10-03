namespace FreeCodeSpot.Attendance.Web.Security;

/// <summary>Names of the authorization policies the pages and the menu use.</summary>
public static class AppPolicies
{
    /// <summary>The signed-in user is linked to an employee record.</summary>
    public const string HasEmployeeRecord = "HasEmployeeRecord";
}
