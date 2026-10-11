namespace FreeCodeSpot.Attendance.Application.Reports;

/// <summary>
/// The result of an export: the .xlsx bytes and a file name, or the errors of a bad range.
/// </summary>
public sealed record ReportExportResult(
    ReportOutcome Outcome,
    byte[]? Content,
    string FileName,
    IReadOnlyDictionary<string, string[]> Errors)
{
    private static readonly IReadOnlyDictionary<string, string[]> NoErrors = new Dictionary<string, string[]>();

    public static ReportExportResult Success(byte[] content, string fileName) =>
        new(ReportOutcome.Success, content, fileName, NoErrors);

    public static ReportExportResult Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        new(ReportOutcome.Invalid, null, string.Empty, errors);
}
