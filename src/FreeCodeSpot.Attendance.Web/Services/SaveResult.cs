namespace FreeCodeSpot.Attendance.Web.Services;

/// <summary>
/// What happened when the Web app asked the API to save an employee.
/// Errors holds the API's validation messages, keyed by field name.
/// </summary>
public sealed record SaveResult(bool Succeeded, bool NotFound, IReadOnlyDictionary<string, string[]> Errors)
{
    private static readonly IReadOnlyDictionary<string, string[]> NoErrors = new Dictionary<string, string[]>();

    public static SaveResult Saved { get; } = new(true, false, NoErrors);

    public static SaveResult Missing { get; } = new(false, true, NoErrors);

    public static SaveResult Invalid(IReadOnlyDictionary<string, string[]> errors) => new(false, false, errors);
}
