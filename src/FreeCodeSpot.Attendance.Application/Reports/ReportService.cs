using FreeCodeSpot.Attendance.Application.Employees;
using FreeCodeSpot.Attendance.Application.Timesheets;

namespace FreeCodeSpot.Attendance.Application.Reports;

/// <summary>
/// Builds the attendance report. It starts from the timesheet of everyone, so the
/// range rules, the days in the time zone and the hours are the ones the timesheets
/// use, and then applies the rules for late arrivals and absences on top.
/// </summary>
public sealed class ReportService(
    TimesheetService timesheets,
    IEmployeeRepository employees,
    TimeProvider timeProvider,
    TimeZoneInfo timeZone,
    ReportOptions options)
{
    /// <summary>
    /// Returns the report between two days, both included. Without a range it returns
    /// the current month up to today. A day counts as an absence only once it is over,
    /// so today never does.
    /// </summary>
    public async Task<ReportResult> GetReportAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        var today = Today();
        if (from is null && to is null)
        {
            from = new DateOnly(today.Year, today.Month, 1);
            to = today;
        }

        var result = await timesheets.GetTimesheetAsync(from, to, employeeId: null, cancellationToken);
        if (result.Timesheet is not { } timesheet)
        {
            return ReportResult.Invalid(result.Errors);
        }

        var roster = await employees.GetAllAsync(includeInactive: true, cancellationToken);
        var sheets = timesheet.Employees.ToDictionary(sheet => sheet.EmployeeId);
        var graceEnd = options.WorkStart.AddMinutes(options.GraceMinutes);

        // The first clock-in of a working day decides whether the employee was late.
        var lateArrivals = timesheet.Employees
            .SelectMany(sheet => sheet.Days
                .Where(day => IsWorkingDay(day.Date))
                .Select(day => (Sheet: sheet, Day: day, ClockIn: day.Entries.Min(entry => entry.ClockInUtc))))
            .Where(first => TimeOfDay(first.ClockIn) > graceEnd)
            .Select(first => new LateArrival(
                first.Sheet.EmployeeId,
                first.Sheet.FullName,
                first.Sheet.Department,
                first.Day.Date,
                first.ClockIn,
                TimeOfDay(first.ClockIn) - options.WorkStart))
            .OrderBy(late => late.Date)
            .ThenBy(late => late.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var daysOver = WorkingDaysBefore(timesheet.From, timesheet.To, today);

        var absences = roster
            .Where(employee => employee.IsActive)
            .Select(employee =>
            {
                var present = sheets.TryGetValue(employee.Id, out var sheet)
                    ? sheet.Days.Select(day => day.Date).ToHashSet()
                    : [];

                return new EmployeeAbsences(
                    employee.Id,
                    employee.FullName,
                    employee.Department,
                    daysOver.Where(day => !present.Contains(day)).ToList());
            })
            .Where(absent => absent.Dates.Count > 0)
            .OrderBy(absent => absent.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Every active employee has a row, even with no records. An inactive employee
        // only shows up when they have records in the range.
        var summary = roster
            .Where(employee => employee.IsActive || sheets.ContainsKey(employee.Id))
            .Select(employee =>
            {
                sheets.TryGetValue(employee.Id, out var sheet);
                return new EmployeeSummary(
                    employee.Id,
                    employee.FullName,
                    employee.Department,
                    sheet?.Days.Count ?? 0,
                    sheet?.TotalWorked ?? TimeSpan.Zero,
                    lateArrivals.Count(late => late.EmployeeId == employee.Id),
                    absences.FirstOrDefault(absent => absent.EmployeeId == employee.Id)?.Dates.Count ?? 0);
            })
            .OrderBy(row => row.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return ReportResult.Success(new AttendanceReport(
            timesheet.From,
            timesheet.To,
            timeZone.Id,
            options.WorkStart,
            options.GraceMinutes,
            summary,
            lateArrivals,
            absences));
    }

    private List<DateOnly> WorkingDaysBefore(DateOnly from, DateOnly to, DateOnly today)
    {
        var days = new List<DateOnly>();
        for (var day = from; day <= to && day < today; day = day.AddDays(1))
        {
            if (IsWorkingDay(day))
            {
                days.Add(day);
            }
        }

        return days;
    }

    private bool IsWorkingDay(DateOnly day) => options.WorkingDays.Contains(day.DayOfWeek);

    private DateOnly Today() => DateOnly.FromDateTime(InZone(timeProvider.GetUtcNow()));

    private TimeOnly TimeOfDay(DateTimeOffset utc) => TimeOnly.FromDateTime(InZone(utc));

    private DateTime InZone(DateTimeOffset utc) => TimeZoneInfo.ConvertTime(utc, timeZone).DateTime;
}
