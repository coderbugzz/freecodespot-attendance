namespace FreeCodeSpot.Attendance.Application.Employees.Import;

/// <summary>
/// One row of the uploaded sheet, as the reader found it. RowNumber is the row number
/// in the spreadsheet, so a message can tell the user where to look. The values can be
/// empty, because nobody checked them yet.
/// </summary>
public sealed record EmployeeImportRow(int RowNumber, string? FullName, string? Department, string? Email);

/// <summary>
/// The rows of a sheet, or the reason the sheet could not be read, for example a file
/// that is not a workbook or has no Email column.
/// </summary>
public sealed record EmployeeSheetReadResult(IReadOnlyList<EmployeeImportRow> Rows, string? Error)
{
    public static EmployeeSheetReadResult Of(IReadOnlyList<EmployeeImportRow> rows) => new(rows, null);

    public static EmployeeSheetReadResult Failed(string error) => new([], error);
}
