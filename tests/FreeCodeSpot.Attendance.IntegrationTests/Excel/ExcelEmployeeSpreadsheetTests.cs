using ClosedXML.Excel;
using FreeCodeSpot.Attendance.Infrastructure.Excel;

namespace FreeCodeSpot.Attendance.IntegrationTests.Excel;

/// <summary>
/// Reads real .xlsx files. The workbooks are built in memory with ClosedXML, so the tests
/// read exactly what Excel would save. They need no database.
/// </summary>
public class ExcelEmployeeSpreadsheetTests
{
    private readonly ExcelEmployeeSpreadsheet spreadsheet = new();

    private static MemoryStream Workbook(Action<IXLWorksheet> fill)
    {
        var workbook = new XLWorkbook();
        fill(workbook.AddWorksheet("Employees"));

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void Read_ReturnsTheRowsWithTheirRowNumbersInTheSheet()
    {
        using var file = Workbook(sheet =>
        {
            sheet.Cell("A1").Value = "Full name";
            sheet.Cell("B1").Value = "Department";
            sheet.Cell("C1").Value = "Email";
            sheet.Cell("A2").Value = "Joy Mendoza";
            sheet.Cell("B2").Value = "Finance";
            sheet.Cell("C2").Value = "joy@example.com";
            sheet.Cell("A3").Value = "Paolo Santos";
            sheet.Cell("B3").Value = "Operations";
            sheet.Cell("C3").Value = "paolo@example.com";
        });

        var result = spreadsheet.Read(file);

        Assert.Null(result.Error);
        Assert.Equal([2, 3], result.Rows.Select(row => row.RowNumber));
        Assert.Equal("Joy Mendoza", result.Rows[0].FullName);
        Assert.Equal("operations", result.Rows[1].Department?.ToLowerInvariant());
        Assert.Equal("paolo@example.com", result.Rows[1].Email);
    }

    [Fact]
    public void Read_FindsTheColumnsByTheirHeadersInAnyOrder_AndIgnoresOtherColumns()
    {
        using var file = Workbook(sheet =>
        {
            sheet.Cell("A1").Value = "Notes";
            sheet.Cell("B1").Value = "EMAIL";
            sheet.Cell("C1").Value = "department";
            sheet.Cell("D1").Value = " Full Name ";
            sheet.Cell("A2").Value = "ignored";
            sheet.Cell("B2").Value = "joy@example.com";
            sheet.Cell("C2").Value = "Finance";
            sheet.Cell("D2").Value = "Joy Mendoza";
        });

        var row = Assert.Single(spreadsheet.Read(file).Rows);

        Assert.Equal(("Joy Mendoza", "Finance", "joy@example.com"), (row.FullName, row.Department, row.Email));
    }

    [Fact]
    public void Read_SkipsRowsThatAreEmpty_AndKeepsTheRowNumbersOfTheOthers()
    {
        using var file = Workbook(sheet =>
        {
            sheet.Cell("A1").Value = "Full name";
            sheet.Cell("B1").Value = "Department";
            sheet.Cell("C1").Value = "Email";
            sheet.Cell("A2").Value = "Joy Mendoza";
            sheet.Cell("B2").Value = "Finance";
            sheet.Cell("C2").Value = "joy@example.com";
            sheet.Cell("A4").Value = "Paolo Santos";
            sheet.Cell("B4").Value = "Operations";
            sheet.Cell("C4").Value = "paolo@example.com";
            sheet.Cell("D6").Value = "   ";
        });

        var rows = spreadsheet.Read(file).Rows;

        Assert.Equal([2, 4], rows.Select(row => row.RowNumber));
    }

    [Fact]
    public void Read_ReadsANumberInACellAsText()
    {
        using var file = Workbook(sheet =>
        {
            sheet.Cell("A1").Value = "Full name";
            sheet.Cell("B1").Value = "Department";
            sheet.Cell("C1").Value = "Email";
            sheet.Cell("A2").Value = "Joy Mendoza";
            sheet.Cell("B2").Value = 42;
            sheet.Cell("C2").Value = "joy@example.com";
        });

        var row = Assert.Single(spreadsheet.Read(file).Rows);

        Assert.Equal("42", row.Department);
    }

    [Fact]
    public void Read_ReportsTheColumnsThatAreMissing()
    {
        using var file = Workbook(sheet =>
        {
            sheet.Cell("A1").Value = "Full name";
            sheet.Cell("B1").Value = "Department";
            sheet.Cell("A2").Value = "Joy Mendoza";
        });

        var result = spreadsheet.Read(file);

        Assert.Empty(result.Rows);
        Assert.Contains("Missing: Email.", result.Error);
    }

    [Fact]
    public void Read_ReportsAnEmptySheet()
    {
        using var file = Workbook(_ => { });

        Assert.Equal("The first sheet of the workbook is empty.", spreadsheet.Read(file).Error);
    }

    [Fact]
    public void Read_ReportsAFileThatIsNotAWorkbook()
    {
        using var file = new MemoryStream("name,department,email\nJoy,Finance,joy@example.com"u8.ToArray());

        Assert.Equal("The file is not a valid .xlsx workbook.", spreadsheet.Read(file).Error);
    }

    [Fact]
    public void CreateTemplate_HasTheThreeHeadersAndTheImportCanReadItBack()
    {
        using var file = new MemoryStream(spreadsheet.CreateTemplate());

        using (var workbook = new XLWorkbook(file))
        {
            var sheet = workbook.Worksheets.First();
            Assert.Equal(
                ["Full name", "Department", "Email"],
                sheet.Row(1).CellsUsed().Select(cell => cell.GetString()));
        }

        file.Position = 0;
        var result = spreadsheet.Read(file);
        Assert.Null(result.Error);
        Assert.Empty(result.Rows);
    }
}
