namespace FreeCodeSpot.Attendance.Application.Security;

/// <summary>
/// The names of the custom claims a signed-in user carries. The claim ties the
/// user to the employee record whose attendance they may see and change.
/// </summary>
public static class AttendanceClaims
{
    public const string EmployeeId = "employee_id";
}
