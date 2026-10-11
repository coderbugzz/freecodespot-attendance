using ClosedXML.Excel;
using FreeCodeSpot.Attendance.Application.Reports;
using FreeCodeSpot.Attendance.Application.Timesheets;
using FreeCodeSpot.Attendance.Infrastructure.Excel;

namespace FreeCodeSpot.Attendance.IntegrationTests.Excel;

/// <summary>
/// Writes a real workbook and checks what is stored in the cells: values with a number
/// format, not text. Numbers are read from the XML inside the file, which is what Excel
/// sees. The report is built by hand, so no database is needed.
/// </summary>
public class ExcelReportWorkbookTests
{
    private const int Summary = 1;
    private const int LateArrivals = 2;
    private const int Absences = 3;
    private const int Records = 4;

    // Two hours ahead of UTC, so a time in the sheet differs from the UTC time.
    private static readonly TimeZoneInfo Plus2 =
        TimeZoneInfo.CreateCustomTimeZone("Test+2", TimeSpan.FromHours(2), "Test+2", "Test+2");

    private static readonly DateTimeOffset ClockIn = new(2026, 3, 2, 6, 25, 0, TimeSpan.Zero);

    private readonly ExcelReportWorkbook writer = new(Plus2);

    private byte[] Create()
    {
        var late = new LateArrival(1, "Anna Cruz", "Engineering", new DateOnly(2026, 3, 2), ClockIn, TimeSpan.FromMinutes(25));

        var report = new AttendanceReport(
            new DateOnly(2026, 3, 2),
            new DateOnly(2026, 3, 3),
            "Test+2",
            new TimeOnly(8, 0),
            10,
            [
                new EmployeeSummary(1, "Anna Cruz", "Engineering", 1, TimeSpan.FromHours(8.5), 1, 1),
                new EmployeeSummary(2, "Marco Diaz", "Support", 1, TimeSpan.FromHours(4), 0, 0)
            ],
            [late],
            [new EmployeeAbsences(1, "Anna Cruz", "Engineering", [new DateOnly(2026, 3, 3)])]);

        var records = new Timesheet(
            new DateOnly(2026, 3, 2),
            new DateOnly(2026, 3, 3),
            "Test+2",
            [
                new EmployeeTimesheet(1, "Anna Cruz", "Engineering",
                    [
                        new TimesheetDay(new DateOnly(2026, 3, 2),
                            [new TimesheetEntry(10, ClockIn, ClockIn.AddHours(8.5), TimeSpan.FromHours(8.5))],
                            TimeSpan.FromHours(8.5))
                    ],
                    TimeSpan.FromHours(8.5)),
                new EmployeeTimesheet(2, "Marco Diaz", "Support",
                    [
                        new TimesheetDay(new DateOnly(2026, 3, 2),
                            [new TimesheetEntry(11, ClockIn, null, null)],
                            TimeSpan.Zero)
                    ],
                    TimeSpan.Zero)
            ]);

        return writer.Create(report, records);
    }

    private static XLWorkbook Open(byte[] xlsx) => new(new MemoryStream(xlsx));

    [Fact]
    public void TheWorkbookHasASheetForEachPartOfTheReport()
    {
        using var workbook = Open(Create());

        Assert.Equal(
            ["Summary", "Late arrivals", "Absences", "Records"],
            workbook.Worksheets.Select(sheet => sheet.Name));
    }

    [Fact]
    public void TheSummaryHoldsHoursAsNumbersWithAHoursFormat()
    {
        var xlsx = Create();
        using var workbook = Open(xlsx);

        // Excel keeps a time as a part of a day, so 8 hours and 30 minutes is 8.5 / 24.
        Assert.Equal(8.5 / 24, XlsxXml.Number(xlsx, Summary, "D2"), precision: 6);
        Assert.Equal("[h]:mm", workbook.Worksheet("Summary").Cell("D2").Style.NumberFormat.Format);
        Assert.Equal(1, XlsxXml.Number(xlsx, Summary, "C2"));
    }

    [Fact]
    public void TheSummaryEndsWithATotalRowOfFormulas()
    {
        var xlsx = Create();
        using var workbook = Open(xlsx);

        Assert.Equal("Total", workbook.Worksheet("Summary").Cell("A4").GetString());
        Assert.Equal("SUM(C2:C3)", XlsxXml.Formula(xlsx, Summary, "C4"));
        Assert.Equal("SUM(D2:D3)", XlsxXml.Formula(xlsx, Summary, "D4"));
        Assert.Equal("[h]:mm", workbook.Worksheet("Summary").Cell("D4").Style.NumberFormat.Format);
    }

    [Fact]
    public void ADateIsStoredAsADateAndATimeIsInTheTimeZoneOfTheReport()
    {
        var xlsx = Create();
        using var workbook = Open(xlsx);
        var late = workbook.Worksheet("Late arrivals");

        Assert.Equal(new DateTime(2026, 3, 2).ToOADate(), XlsxXml.Number(xlsx, LateArrivals, "A2"));
        Assert.Equal("yyyy-mm-dd", late.Cell("A2").Style.NumberFormat.Format);

        // 06:25 UTC is 08:25 in Test+2. The cell has a time-of-day format, so Excel shows 08:25.
        var serial = XlsxXml.Number(xlsx, LateArrivals, "D2");
        Assert.Equal((8 * 60 + 25) / 1440.0, serial - Math.Floor(serial), precision: 6);
        Assert.Equal("hh:mm", late.Cell("D2").Style.NumberFormat.Format);

        Assert.Equal(25, XlsxXml.Number(xlsx, LateArrivals, "E2"));
    }

    [Fact]
    public void TheAbsencesHaveARowForEachDay()
    {
        var xlsx = Create();
        using var workbook = Open(xlsx);
        var absences = workbook.Worksheet("Absences");

        Assert.Equal("Anna Cruz", absences.Cell("A2").GetString());
        Assert.Equal(new DateTime(2026, 3, 3).ToOADate(), XlsxXml.Number(xlsx, Absences, "C2"));
        Assert.True(absences.Cell("A3").IsEmpty());
    }

    [Fact]
    public void ARecordThatIsStillOpenHasNoClockOutAndNoHours()
    {
        var xlsx = Create();
        using var workbook = Open(xlsx);
        var records = workbook.Worksheet("Records");

        Assert.Equal(8.5 / 24, XlsxXml.Number(xlsx, Records, "F2"), precision: 6);
        Assert.Equal("Marco Diaz", records.Cell("A3").GetString());
        Assert.False(records.Cell("D3").IsEmpty());
        Assert.True(records.Cell("E3").IsEmpty());
        Assert.True(records.Cell("F3").IsEmpty());
    }

    [Fact]
    public void EverySheetHasAHeaderRowThatIsFrozenAndFiltered()
    {
        using var workbook = Open(Create());

        foreach (var sheet in workbook.Worksheets)
        {
            Assert.True(sheet.Cell("A1").Style.Font.Bold, sheet.Name);
            Assert.True(sheet.AutoFilter.IsEnabled, sheet.Name);
            Assert.Equal(1, sheet.SheetView.SplitRow);
        }
    }

    [Fact]
    public void AReportWithNothingInItStillHasTheHeaders()
    {
        var empty = new AttendanceReport(new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 2), "Test+2", new TimeOnly(8, 0), 10, [], [], []);
        var records = new Timesheet(new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 2), "Test+2", []);

        using var workbook = Open(writer.Create(empty, records));

        Assert.Equal("Employee", workbook.Worksheet("Summary").Cell("A1").GetString());
        Assert.Equal("Date", workbook.Worksheet("Late arrivals").Cell("A1").GetString());
        Assert.True(workbook.Worksheet("Summary").Cell("A2").IsEmpty());
    }
}
