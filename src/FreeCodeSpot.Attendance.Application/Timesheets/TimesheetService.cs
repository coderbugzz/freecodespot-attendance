using FreeCodeSpot.Attendance.Application.Employees;

namespace FreeCodeSpot.Attendance.Application.Timesheets;

/// <summary>
/// Builds timesheets. The database does the filtering by date and employee. The
/// grouping into days and the hours are worked out here, because a day depends on
/// the time zone, and that is easier to test in code than in a query.
/// </summary>
public sealed class TimesheetService(
    ITimesheetRepository timesheets,
    IEmployeeRepository employees,
    TimeProvider timeProvider,
    TimeZoneInfo timeZone)
{
    public const int MaxDays = 31;
    public const int DefaultDays = 7;

    /// <summary>
    /// Returns the records clocked in between two days, both included. Without a
    /// range it returns the last seven days, today included. A record that runs
    /// past midnight counts on the day it was clocked in.
    /// </summary>
    public async Task<TimesheetResult> GetTimesheetAsync(
        DateOnly? from,
        DateOnly? to,
        int? employeeId,
        CancellationToken cancellationToken = default)
    {
        var last = to ?? Today();
        var first = from ?? last.AddDays(1 - DefaultDays);

        var errors = Validate(first, last);
        if (errors.Count > 0)
        {
            return TimesheetResult.Invalid(errors);
        }

        if (employeeId is { } id && await employees.GetByIdAsync(id, cancellationToken) is null)
        {
            return TimesheetResult.NotFound();
        }

        var records = await timesheets.GetRecordsAsync(
            StartOfDayUtc(first),
            StartOfDayUtc(last.AddDays(1)),
            employeeId,
            cancellationToken);

        return TimesheetResult.Success(new Timesheet(first, last, timeZone.Id, Group(records)));
    }

    private List<EmployeeTimesheet> Group(IReadOnlyList<TimesheetRecord> records) =>
        records
            .GroupBy(record => record.EmployeeId)
            .Select(employeeRecords =>
            {
                var days = employeeRecords
                    .GroupBy(record => DayOf(record.ClockInUtc))
                    .OrderBy(day => day.Key)
                    .Select(day =>
                    {
                        var entries = day
                            .OrderBy(record => record.ClockInUtc)
                            .Select(record => new TimesheetEntry(
                                record.RecordId,
                                record.ClockInUtc,
                                record.ClockOutUtc,
                                record.ClockOutUtc - record.ClockInUtc))
                            .ToList();

                        return new TimesheetDay(day.Key, entries, Total(entries.Select(entry => entry.Duration)));
                    })
                    .ToList();

                var employee = employeeRecords.First();
                return new EmployeeTimesheet(
                    employee.EmployeeId,
                    employee.FullName,
                    employee.Department,
                    days,
                    Total(days.Select(day => (TimeSpan?)day.Worked)));
            })
            .OrderBy(employee => employee.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    // Records that are still open have no duration, so they add nothing to a total.
    private static TimeSpan Total(IEnumerable<TimeSpan?> durations) =>
        TimeSpan.FromTicks(durations.Sum(duration => duration?.Ticks ?? 0));

    private static Dictionary<string, string[]> Validate(DateOnly first, DateOnly last)
    {
        var errors = new Dictionary<string, string[]>();

        if (last < first)
            errors["To"] = ["The end date must not be before the start date."];
        else if (last.DayNumber - first.DayNumber + 1 > MaxDays)
            errors["To"] = [$"The range can be at most {MaxDays} days."];

        return errors;
    }

    private DateOnly Today() => DayOf(timeProvider.GetUtcNow());

    private DateOnly DayOf(DateTimeOffset utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utc, timeZone).DateTime);

    // Midnight of a day in the time zone, as a UTC time. A few zones skip midnight when
    // the clocks change, so a midnight that does not exist moves to the hour after it.
    private DateTimeOffset StartOfDayUtc(DateOnly day)
    {
        var midnight = day.ToDateTime(TimeOnly.MinValue);
        if (timeZone.IsInvalidTime(midnight))
        {
            midnight = midnight.AddHours(1);
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(midnight, timeZone), TimeSpan.Zero);
    }
}
