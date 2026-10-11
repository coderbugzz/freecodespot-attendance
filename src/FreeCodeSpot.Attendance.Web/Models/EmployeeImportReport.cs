namespace FreeCodeSpot.Attendance.Web.Models;

/// <summary>
/// What the API answered to an upload of the employee sheet. Saved is false for a preview,
/// where nothing was written, and true after an import.
/// </summary>
public record EmployeeImportReport(bool Saved, IReadOnlyList<ImportRowResult> Rows, int ValidCount, int InvalidCount);

/// <summary>One row of the sheet. RowNumber is the row number in the spreadsheet. EmployeeId is set once the row is imported.</summary>
public record ImportRowResult(
    int RowNumber,
    string FullName,
    string Department,
    string Email,
    IReadOnlyList<string> Errors,
    int? EmployeeId,
    bool IsValid);
