using FreeCodeSpot.Attendance.Application.Timesheets;

namespace FreeCodeSpot.Attendance.Application.Reports;

/// <summary>
/// Exports the attendance report. The range rules and the numbers are the ones of
/// ReportService, so the workbook and the Reports page can never disagree.
/// </summary>
public sealed class ReportExportService(ReportService reports, TimesheetService timesheets, IReportWorkbook workbook)
{
    public async Task<ReportExportResult> ExportAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        var result = await reports.GetReportAsync(from, to, cancellationToken);
        if (result.Report is not { } report)
        {
            return ReportExportResult.Invalid(result.Errors);
        }

        // The report knows its own range now, even when the caller sent none.
        var records = await timesheets.GetTimesheetAsync(report.From, report.To, employeeId: null, cancellationToken);

        return ReportExportResult.Success(
            workbook.Create(report, records.Timesheet!),
            $"attendance-report-{report.From:yyyy-MM-dd}-to-{report.To:yyyy-MM-dd}.xlsx");
    }
}
