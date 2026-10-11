namespace FreeCodeSpot.Attendance.Application.Reports;

public enum ReportOutcome
{
    Success,
    Invalid
}

/// <summary>
/// The result of a report request. Errors is keyed by parameter name, like the
/// errors of a timesheet request.
/// </summary>
public sealed record ReportResult(
    ReportOutcome Outcome,
    AttendanceReport? Report,
    IReadOnlyDictionary<string, string[]> Errors)
{
    private static readonly IReadOnlyDictionary<string, string[]> NoErrors = new Dictionary<string, string[]>();

    public static ReportResult Success(AttendanceReport report) => new(ReportOutcome.Success, report, NoErrors);

    public static ReportResult Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        new(ReportOutcome.Invalid, null, errors);
}
