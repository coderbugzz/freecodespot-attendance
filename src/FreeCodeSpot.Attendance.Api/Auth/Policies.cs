namespace FreeCodeSpot.Attendance.Api.Auth;

/// <summary>Names of the authorization policies the endpoints refer to.</summary>
public static class Policies
{
    public const string ManagerOnly = "ManagerOnly";
    public const string HasEmployeeRecord = "HasEmployeeRecord";
}
