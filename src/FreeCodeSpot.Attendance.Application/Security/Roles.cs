namespace FreeCodeSpot.Attendance.Application.Security;

/// <summary>
/// The role names used by the Identity store, the API policies and the Blazor
/// pages. Keeping them in one place stops a typo from silently locking people out.
/// </summary>
public static class Roles
{
    public const string Manager = "Manager";
    public const string Employee = "Employee";

    public static IReadOnlyList<string> All { get; } = [Manager, Employee];
}
