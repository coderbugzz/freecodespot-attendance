using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using FreeCodeSpot.Attendance.Application;
using FreeCodeSpot.Attendance.Application.Attendance;
using FreeCodeSpot.Attendance.Application.Employees.Import;
using FreeCodeSpot.Attendance.Domain.Attendance;
using FreeCodeSpot.Attendance.Domain.Employees;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FreeCodeSpot.Attendance.IntegrationTests.Excel;

/// <summary>
/// Calls the export and import routes over HTTP, with real .xlsx files in both directions.
/// Every test uses its own emails, and the records of the export are saved on fixed days
/// in March 2026, so no test depends on the others or on today's date.
/// </summary>
[Collection(AttendanceApiCollection.Name)]
public class ExcelEndpointsTests(AttendanceApiFactory factory)
{
    private static readonly DateTimeOffset Monday = new(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);

    private static string NewEmail() => $"{Guid.NewGuid():N}@import.local";

    private static byte[] Workbook(params (string Name, string Department, string Email)[] employees)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Employees");
        sheet.Cell("A1").Value = "Full name";
        sheet.Cell("B1").Value = "Department";
        sheet.Cell("C1").Value = "Email";

        for (var i = 0; i < employees.Length; i++)
        {
            sheet.Cell(i + 2, 1).Value = employees[i].Name;
            sheet.Cell(i + 2, 2).Value = employees[i].Department;
            sheet.Cell(i + 2, 3).Value = employees[i].Email;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static MultipartFormDataContent Upload(byte[] file)
    {
        var content = new MultipartFormDataContent();
        var part = new ByteArrayContent(file);
        part.Headers.ContentType = new MediaTypeHeaderValue(ExcelFile.ContentType);
        content.Add(part, "file", "employees.xlsx");
        return content;
    }

    [Fact]
    public async Task ExportReport_ReturnsAnXlsxFileWithTheRecordsOfTheRange()
    {
        using var manager = await factory.CreateSignedInClientAsync(AttendanceApiFactory.ManagerEmail);
        var (_, employee) = await factory.CreateLinkedEmployeeClientAsync();
        await SaveAsync(employee.Id, Monday.AddMinutes(25), Monday.AddHours(8));

        var response = await manager.GetAsync("/api/reports/export?from=2026-03-02&to=2026-03-06");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ExcelFile.ContentType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attendance-report-2026-03-02-to-2026-03-06.xlsx", response.Content.Headers.ContentDisposition?.FileName);

        var xlsx = await response.Content.ReadAsByteArrayAsync();
        using var workbook = new XLWorkbook(new MemoryStream(xlsx));
        Assert.Equal(["Summary", "Late arrivals", "Absences", "Records"], workbook.Worksheets.Select(sheet => sheet.Name));

        // The employee clocked in at 08:25 and out at 16:00. That is one row with 7h 35m and one late arrival.
        var row = workbook.Worksheet("Summary").RowsUsed()
            .Single(summaryRow => summaryRow.Cell(1).GetString() == employee.FullName
                && XlsxXml.Number(xlsx, 1, $"C{summaryRow.RowNumber()}") == 1);
        Assert.Equal((7 * 60 + 35) / 1440.0, XlsxXml.Number(xlsx, 1, $"D{row.RowNumber()}"), precision: 6);
        Assert.Equal(1, XlsxXml.Number(xlsx, 1, $"E{row.RowNumber()}"));
    }

    [Fact]
    public async Task ExportReport_Returns400ForARangeLongerThanThirtyOneDays()
    {
        using var manager = await factory.CreateSignedInClientAsync(AttendanceApiFactory.ManagerEmail);

        var response = await manager.GetAsync("/api/reports/export?from=2026-01-01&to=2026-03-01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ExportReport_Returns403ForAnEmployee()
    {
        var (employee, _) = await factory.CreateLinkedEmployeeClientAsync();

        var response = await employee.GetAsync("/api/reports/export");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Template_IsAWorkbookWithTheThreeHeaders()
    {
        using var manager = await factory.CreateSignedInClientAsync(AttendanceApiFactory.ManagerEmail);

        var response = await manager.GetAsync("/api/employees/import/template");

        Assert.Equal(ExcelFile.ContentType, response.Content.Headers.ContentType?.MediaType);
        using var workbook = new XLWorkbook(await response.Content.ReadAsStreamAsync());
        Assert.Equal(
            ["Full name", "Department", "Email"],
            workbook.Worksheets.First().Row(1).CellsUsed().Select(cell => cell.GetString()));
    }

    [Fact]
    public async Task Preview_ChecksTheRowsAndSavesNothing()
    {
        using var manager = await factory.CreateSignedInClientAsync(AttendanceApiFactory.ManagerEmail);
        var good = NewEmail();
        var file = Workbook(("Joy Preview", "Finance", good), ("", "Finance", NewEmail()));

        var response = await manager.PostAsync("/api/employees/import/preview", Upload(file));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<EmployeeImportReport>();
        Assert.False(report!.Saved);
        Assert.Equal([true, false], report.Rows.Select(row => row.IsValid));
        Assert.Equal(["Name is required."], report.Rows[1].Errors);

        var roster = await manager.GetFromJsonAsync<List<Employee>>("/api/employees?includeInactive=true");
        Assert.DoesNotContain(roster!, employee => employee.Email == good);
    }

    [Fact]
    public async Task Import_SavesTheRowsThatAreFineAndReportsTheOthers()
    {
        using var manager = await factory.CreateSignedInClientAsync(AttendanceApiFactory.ManagerEmail);
        var good = NewEmail();
        var file = Workbook(("Joy Imported", "Finance", good), ("Anna Again", "Finance", "anna@freecodespot.local"));

        var response = await manager.PostAsync("/api/employees/import", Upload(file));

        var report = await response.Content.ReadFromJsonAsync<EmployeeImportReport>();
        Assert.True(report!.Saved);
        Assert.Equal(1, report.ValidCount);
        Assert.Equal(["Another employee already uses this email."], report.Rows[1].Errors);

        var roster = await manager.GetFromJsonAsync<List<Employee>>("/api/employees?includeInactive=true");
        var joy = Assert.Single(roster!, employee => employee.Email == good);
        Assert.Equal(report.Rows[0].EmployeeId, joy.Id);
        Assert.True(joy.IsActive);
    }

    [Fact]
    public async Task Import_ASecondUploadOfTheSameFileSavesNothingNew()
    {
        using var manager = await factory.CreateSignedInClientAsync(AttendanceApiFactory.ManagerEmail);
        var file = Workbook(("Joy Twice", "Finance", NewEmail()));

        await manager.PostAsync("/api/employees/import", Upload(file));
        var second = await (await manager.PostAsync("/api/employees/import", Upload(file)))
            .Content.ReadFromJsonAsync<EmployeeImportReport>();

        Assert.Equal(0, second!.ValidCount);
        Assert.Equal(["Another employee already uses this email."], second.Rows[0].Errors);
    }

    [Fact]
    public async Task Import_Returns400ForAFileThatIsNotAWorkbook()
    {
        using var manager = await factory.CreateSignedInClientAsync(AttendanceApiFactory.ManagerEmail);

        var response = await manager.PostAsync("/api/employees/import", Upload("a,b,c"u8.ToArray()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Equal(["The file is not a valid .xlsx workbook."], problem!.Errors["file"]);
    }

    [Fact]
    public async Task Import_Returns400WithoutAFile()
    {
        using var manager = await factory.CreateSignedInClientAsync(AttendanceApiFactory.ManagerEmail);

        var response = await manager.PostAsync("/api/employees/import", new MultipartFormDataContent());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Import_Returns400ForAFileOverOneMegabyte()
    {
        using var manager = await factory.CreateSignedInClientAsync(AttendanceApiFactory.ManagerEmail);
        var big = new byte[EmployeeImportService.MaxFileBytes + 1];

        var response = await manager.PostAsync("/api/employees/import/preview", Upload(big));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Equal(["The file can be at most 1 MB."], problem!.Errors["file"]);
    }

    [Fact]
    public async Task Import_Returns403ForAnEmployee()
    {
        var (employee, _) = await factory.CreateLinkedEmployeeClientAsync();

        var response = await employee.PostAsync("/api/employees/import", Upload(Workbook(("Joy", "Finance", NewEmail()))));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Import_Returns401WithoutAToken()
    {
        var response = await factory.CreateClient().PostAsync("/api/employees/import", Upload(Workbook()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task SaveAsync(int employeeId, DateTimeOffset clockIn, DateTimeOffset? clockOut)
    {
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAttendanceRepository>();

        var saved = await repository.TryAddAsync(new AttendanceRecord(0, employeeId, clockIn, clockOut));
        Assert.NotNull(saved);
    }
}
