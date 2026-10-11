using ClosedXML.Excel;
using FreeCodeSpot.Attendance.Application.Reports;
using FreeCodeSpot.Attendance.Application.Timesheets;

namespace FreeCodeSpot.Attendance.Infrastructure.Excel;

/// <summary>
/// Writes the attendance report as a real .xlsx workbook with ClosedXML. Dates, times and
/// hours are saved as values with a number format, not as text, so Excel can sort,
/// filter and add them up. Times are shown in the time zone the report used.
/// </summary>
public sealed class ExcelReportWorkbook(TimeZoneInfo timeZone) : IReportWorkbook
{
    private const string DateFormat = "yyyy-mm-dd";
    private const string TimeFormat = "hh:mm";
    private const string DateTimeFormat = "yyyy-mm-dd hh:mm";
    private const string HoursFormat = "[h]:mm";

    public byte[] Create(AttendanceReport report, Timesheet records)
    {
        using var workbook = new XLWorkbook();

        AddSummary(workbook.AddWorksheet("Summary"), report);
        AddLateArrivals(workbook.AddWorksheet("Late arrivals"), report);
        AddAbsences(workbook.AddWorksheet("Absences"), report);
        AddRecords(workbook.AddWorksheet("Records"), records);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void AddSummary(IXLWorksheet sheet, AttendanceReport report)
    {
        WriteHeader(sheet, "Employee", "Department", "Days worked", "Hours worked", "Late", "Absent");

        var row = 2;
        foreach (var employee in report.Summary)
        {
            sheet.Cell(row, 1).Value = employee.FullName;
            sheet.Cell(row, 2).Value = employee.Department;
            sheet.Cell(row, 3).Value = employee.DaysWorked;
            sheet.Cell(row, 4).Value = employee.TotalWorked;
            sheet.Cell(row, 4).Style.NumberFormat.Format = HoursFormat;
            sheet.Cell(row, 5).Value = employee.LateCount;
            sheet.Cell(row, 6).Value = employee.AbsentCount;
            row++;
        }

        // A total row with real formulas, so the totals follow the cells if somebody edits them.
        if (report.Summary.Count > 0)
        {
            var last = row - 1;
            sheet.Cell(row, 1).Value = "Total";
            foreach (var column in new[] { 3, 4, 5, 6 })
            {
                var letter = XLHelper.GetColumnLetterFromNumber(column);
                sheet.Cell(row, column).FormulaA1 = $"SUM({letter}2:{letter}{last})";
            }

            sheet.Cell(row, 4).Style.NumberFormat.Format = HoursFormat;
            sheet.Row(row).Style.Font.Bold = true;
        }

        Finish(sheet, columns: 6, lastDataRow: row - (report.Summary.Count > 0 ? 1 : 0));
    }

    private void AddLateArrivals(IXLWorksheet sheet, AttendanceReport report)
    {
        WriteHeader(sheet, "Date", "Employee", "Department", "Clocked in", "Minutes late");

        var row = 2;
        foreach (var late in report.LateArrivals)
        {
            sheet.Cell(row, 1).Value = late.Date.ToDateTime(TimeOnly.MinValue);
            sheet.Cell(row, 1).Style.NumberFormat.Format = DateFormat;
            sheet.Cell(row, 2).Value = late.FullName;
            sheet.Cell(row, 3).Value = late.Department;
            sheet.Cell(row, 4).Value = InZone(late.ClockInUtc);
            sheet.Cell(row, 4).Style.NumberFormat.Format = TimeFormat;
            sheet.Cell(row, 5).Value = (int)late.Late.TotalMinutes;
            row++;
        }

        Finish(sheet, columns: 5, lastDataRow: row - 1);
    }

    private static void AddAbsences(IXLWorksheet sheet, AttendanceReport report)
    {
        WriteHeader(sheet, "Employee", "Department", "Date");

        // One row for each absent day, so the sheet can be filtered by employee or by date.
        var row = 2;
        foreach (var absent in report.Absences)
        {
            foreach (var date in absent.Dates)
            {
                sheet.Cell(row, 1).Value = absent.FullName;
                sheet.Cell(row, 2).Value = absent.Department;
                sheet.Cell(row, 3).Value = date.ToDateTime(TimeOnly.MinValue);
                sheet.Cell(row, 3).Style.NumberFormat.Format = DateFormat;
                row++;
            }
        }

        Finish(sheet, columns: 3, lastDataRow: row - 1);
    }

    private void AddRecords(IXLWorksheet sheet, Timesheet records)
    {
        WriteHeader(sheet, "Employee", "Department", "Date", "Clock in", "Clock out", "Hours");

        var row = 2;
        foreach (var employee in records.Employees)
        {
            foreach (var day in employee.Days)
            {
                foreach (var entry in day.Entries)
                {
                    sheet.Cell(row, 1).Value = employee.FullName;
                    sheet.Cell(row, 2).Value = employee.Department;
                    sheet.Cell(row, 3).Value = day.Date.ToDateTime(TimeOnly.MinValue);
                    sheet.Cell(row, 3).Style.NumberFormat.Format = DateFormat;
                    sheet.Cell(row, 4).Value = InZone(entry.ClockInUtc);
                    sheet.Cell(row, 4).Style.NumberFormat.Format = DateTimeFormat;

                    // A record that is still open has no clock-out time and no hours. The cells stay empty.
                    if (entry.ClockOutUtc is { } clockOut && entry.Duration is { } duration)
                    {
                        sheet.Cell(row, 5).Value = InZone(clockOut);
                        sheet.Cell(row, 5).Style.NumberFormat.Format = DateTimeFormat;
                        sheet.Cell(row, 6).Value = duration;
                        sheet.Cell(row, 6).Style.NumberFormat.Format = HoursFormat;
                    }

                    row++;
                }
            }
        }

        Finish(sheet, columns: 6, lastDataRow: row - 1);
    }

    private static void WriteHeader(IXLWorksheet sheet, params string[] names)
    {
        for (var column = 0; column < names.Length; column++)
        {
            sheet.Cell(1, column + 1).Value = names[column];
        }

        var header = sheet.Range(1, 1, 1, names.Length);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.LightGray;
        sheet.SheetView.FreezeRows(1);
    }

    // The filter covers the header and the data, but not the total row.
    private static void Finish(IXLWorksheet sheet, int columns, int lastDataRow)
    {
        sheet.Range(1, 1, lastDataRow, columns).SetAutoFilter();
        sheet.Columns(1, columns).AdjustToContents();
    }

    private DateTime InZone(DateTimeOffset utc) => TimeZoneInfo.ConvertTime(utc, timeZone).DateTime;
}
