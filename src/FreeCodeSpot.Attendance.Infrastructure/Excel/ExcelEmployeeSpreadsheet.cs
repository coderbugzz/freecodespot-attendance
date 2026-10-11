using ClosedXML.Excel;
using FreeCodeSpot.Attendance.Application.Employees.Import;

namespace FreeCodeSpot.Attendance.Infrastructure.Excel;

/// <summary>
/// Reads the employee import sheet and writes its template with ClosedXML. The first sheet
/// is the one that counts. The first row has the headers, in any order, and other columns
/// are ignored. Rows with nothing in the three columns are skipped, so a few empty rows at
/// the end of a sheet do no harm.
/// </summary>
public sealed class ExcelEmployeeSpreadsheet : IEmployeeSpreadsheet
{
    private const string FullNameHeader = "Full name";
    private const string DepartmentHeader = "Department";
    private const string EmailHeader = "Email";

    public EmployeeSheetReadResult Read(Stream xlsx)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(xlsx);
        }
        catch (Exception)
        {
            // ClosedXML throws a different exception for each way a file can be broken.
            return EmployeeSheetReadResult.Failed("The file is not a valid .xlsx workbook.");
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault();
            var header = sheet?.FirstRowUsed();
            if (sheet is null || header is null)
            {
                return EmployeeSheetReadResult.Failed("The first sheet of the workbook is empty.");
            }

            var fullName = FindColumn(header, FullNameHeader);
            var department = FindColumn(header, DepartmentHeader);
            var email = FindColumn(header, EmailHeader);

            var missing = new[] { (FullNameHeader, fullName), (DepartmentHeader, department), (EmailHeader, email) }
                .Where(column => column.Item2 is null)
                .Select(column => column.Item1)
                .ToList();
            if (missing.Count > 0)
            {
                return EmployeeSheetReadResult.Failed(
                    $"The first row must have the columns {FullNameHeader}, {DepartmentHeader} and {EmailHeader}. Missing: {string.Join(", ", missing)}.");
            }

            var rows = sheet.RowsUsed()
                .Where(row => row.RowNumber() > header.RowNumber())
                .Select(row => new EmployeeImportRow(
                    row.RowNumber(),
                    row.Cell(fullName!.Value).GetString(),
                    row.Cell(department!.Value).GetString(),
                    row.Cell(email!.Value).GetString()))
                .Where(row => !string.IsNullOrWhiteSpace(row.FullName)
                    || !string.IsNullOrWhiteSpace(row.Department)
                    || !string.IsNullOrWhiteSpace(row.Email))
                .ToList();

            return EmployeeSheetReadResult.Of(rows);
        }
    }

    public byte[] CreateTemplate()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Employees");

        string[] headers = [FullNameHeader, DepartmentHeader, EmailHeader];
        for (var column = 0; column < headers.Length; column++)
        {
            var cell = sheet.Cell(1, column + 1);
            cell.Value = headers[column];
            cell.Style.Font.Bold = true;
            sheet.Column(column + 1).Width = column == 2 ? 36 : 28;
        }

        sheet.SheetView.FreezeRows(1);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static int? FindColumn(IXLRow header, string name) =>
        header.CellsUsed()
            .FirstOrDefault(cell => string.Equals(cell.GetString().Trim(), name, StringComparison.OrdinalIgnoreCase))
            ?.Address.ColumnNumber;
}
