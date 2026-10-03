namespace FreeCodeSpot.Attendance.Application.Timesheets;

public enum TimesheetOutcome
{
    Success,
    NotFound,
    Invalid
}

/// <summary>
/// The result of a timesheet request. Errors is keyed by parameter name, like the
/// validation errors of the employee forms.
/// </summary>
public sealed record TimesheetResult(
    TimesheetOutcome Outcome,
    Timesheet? Timesheet,
    IReadOnlyDictionary<string, string[]> Errors)
{
    private static readonly IReadOnlyDictionary<string, string[]> NoErrors = new Dictionary<string, string[]>();

    public static TimesheetResult Success(Timesheet timesheet) => new(TimesheetOutcome.Success, timesheet, NoErrors);

    public static TimesheetResult NotFound() => new(TimesheetOutcome.NotFound, null, NoErrors);

    public static TimesheetResult Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        new(TimesheetOutcome.Invalid, null, errors);
}
