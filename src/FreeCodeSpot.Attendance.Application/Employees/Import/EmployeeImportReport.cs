namespace FreeCodeSpot.Attendance.Application.Employees.Import;

/// <summary>
/// What happened to one row. Errors is empty when the row is fine. EmployeeId is set
/// only after the row was really imported.
/// </summary>
public sealed record ImportRowResult(
    int RowNumber,
    string FullName,
    string Department,
    string Email,
    IReadOnlyList<string> Errors,
    int? EmployeeId)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// The rows of an upload and what happened to each of them. Saved is false for a preview,
/// where nothing was written, and true after an import.
/// </summary>
public sealed record EmployeeImportReport(bool Saved, IReadOnlyList<ImportRowResult> Rows)
{
    public int ValidCount => Rows.Count(row => row.IsValid);

    public int InvalidCount => Rows.Count - ValidCount;
}

public enum EmployeeImportOutcome
{
    Success,
    InvalidFile
}

/// <summary>
/// The result of a preview or an import. A file that cannot be used at all, for example
/// a file with no Email column, is InvalidFile with the reason in Error.
/// </summary>
public sealed record EmployeeImportResult(
    EmployeeImportOutcome Outcome,
    EmployeeImportReport? Report,
    string? Error)
{
    public static EmployeeImportResult Success(EmployeeImportReport report) =>
        new(EmployeeImportOutcome.Success, report, null);

    public static EmployeeImportResult InvalidFile(string error) =>
        new(EmployeeImportOutcome.InvalidFile, null, error);
}
