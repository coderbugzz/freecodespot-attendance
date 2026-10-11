using FreeCodeSpot.Attendance.Application.Employees;
using FreeCodeSpot.Attendance.Application.Reports;
using FreeCodeSpot.Attendance.Application.Timesheets;
using FreeCodeSpot.Attendance.Domain.Employees;

namespace FreeCodeSpot.Attendance.UnitTests.Reports;

public class ReportServiceTests
{
    private sealed class FakeClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>Only the roster list the report uses does real work.</summary>
    private sealed class FakeEmployees(params Employee[] seed) : IEmployeeRepository
    {
        public Task<IReadOnlyList<Employee>> GetAllAsync(bool includeInactive, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Employee>>(seed.Where(employee => includeInactive || employee.IsActive).ToList());

        public Task<Employee?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(seed.FirstOrDefault(employee => employee.Id == id));

        public Task<bool> EmailExistsAsync(string email, int? exceptId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Employee> AddAsync(Employee employee, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UpdateAsync(Employee employee, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    /// <summary>A list-backed source that filters and orders like the SQL query does.</summary>
    private sealed class FakeTimesheets(params TimesheetRecord[] seed) : ITimesheetRepository
    {
        public Task<IReadOnlyList<TimesheetRecord>> GetRecordsAsync(
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            int? employeeId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TimesheetRecord>>(seed
                .Where(record => record.ClockInUtc >= fromUtc && record.ClockInUtc < toUtc)
                .Where(record => employeeId is null || record.EmployeeId == employeeId)
                .OrderBy(record => record.FullName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(record => record.ClockInUtc)
                .ToList());
    }

    // A time zone eight hours ahead of UTC, so 08:00 on a day is 00:00 UTC of the same day.
    private static readonly TimeZoneInfo Plus8 =
        TimeZoneInfo.CreateCustomTimeZone("Test+8", TimeSpan.FromHours(8), "Test+8", "Test+8");

    private static readonly Employee Anna = new(1, "Anna Cruz", "Engineering", "anna@example.com");
    private static readonly Employee Marco = new(2, "Marco Diaz", "Support", "marco@example.com");
    private static readonly Employee Paolo = new(3, "Paolo Santos", "Finance", "paolo@example.com", IsActive: false);

    // Monday 2026-10-05, 18:00 in Plus8. Thursday the 1st and Friday the 2nd are over, the 3rd and 4th are a weekend.
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 10, 0, 0, TimeSpan.Zero);

    private static DateOnly Day(int day) => new(2026, 10, day);

    /// <summary>A record clocked in at a local time of day (Plus8) and, when given, out a number of hours later.</summary>
    private static TimesheetRecord Record(Employee employee, int id, int day, int hour, int minute, double? hours = 8) =>
        new(
            employee.Id,
            employee.FullName,
            employee.Department,
            id,
            new DateTimeOffset(2026, 10, day, hour, minute, 0, TimeSpan.FromHours(8)).ToUniversalTime(),
            hours is { } worked
                ? new DateTimeOffset(2026, 10, day, hour, minute, 0, TimeSpan.FromHours(8)).ToUniversalTime().AddHours(worked)
                : null);

    private static ReportService CreateService(Employee[] roster, TimesheetRecord[] records, ReportOptions? options = null) =>
        new(
            new TimesheetService(new FakeTimesheets(records), new FakeEmployees(roster), new FakeClock(Now), Plus8),
            new FakeEmployees(roster),
            new FakeClock(Now),
            Plus8,
            options ?? new ReportOptions { WorkingDays = ReportOptions.DefaultWorkingDays });

    private static async Task<AttendanceReport> ReportAsync(
        Employee[] roster,
        TimesheetRecord[] records,
        DateOnly? from = null,
        DateOnly? to = null,
        ReportOptions? options = null)
    {
        var result = await CreateService(roster, records, options).GetReportAsync(from ?? Day(1), to ?? Day(5));

        Assert.Equal(ReportOutcome.Success, result.Outcome);
        return result.Report!;
    }

    [Fact]
    public async Task AClockInAfterTheGracePeriodIsLate_AndLateIsCountedFromTheStartOfWork()
    {
        var report = await ReportAsync([Anna], [Record(Anna, 1, 1, 8, 11)]);

        var late = Assert.Single(report.LateArrivals);
        Assert.Equal(Anna.Id, late.EmployeeId);
        Assert.Equal(Day(1), late.Date);
        Assert.Equal(TimeSpan.FromMinutes(11), late.Late);
    }

    [Fact]
    public async Task AClockInAtTheEndOfTheGracePeriodIsOnTime()
    {
        var report = await ReportAsync([Anna], [Record(Anna, 1, 1, 8, 10)]);

        Assert.Empty(report.LateArrivals);
    }

    [Fact]
    public async Task OnlyTheFirstClockInOfTheDayDecidesIfTheEmployeeWasLate()
    {
        var report = await ReportAsync(
            [Anna],
            [Record(Anna, 1, 1, 8, 0, hours: 4), Record(Anna, 2, 1, 13, 0, hours: 4)]);

        Assert.Empty(report.LateArrivals);
    }

    [Fact]
    public async Task AClockInOnADayThatIsNotAWorkingDayIsNeverLate()
    {
        var report = await ReportAsync([Anna], [Record(Anna, 1, 3, 11, 0)]);

        Assert.Empty(report.LateArrivals);
    }

    [Fact]
    public async Task TheWorkStartAndTheGracePeriodComeFromTheOptions()
    {
        var options = new ReportOptions
        {
            WorkStart = new TimeOnly(9, 0),
            GraceMinutes = 0,
            WorkingDays = ReportOptions.DefaultWorkingDays
        };

        var report = await ReportAsync(
            [Anna],
            [Record(Anna, 1, 1, 8, 30), Record(Anna, 2, 2, 9, 1)],
            options: options);

        var late = Assert.Single(report.LateArrivals);
        Assert.Equal(Day(2), late.Date);
        Assert.Equal(TimeSpan.FromMinutes(1), late.Late);
        Assert.Equal(new TimeOnly(9, 0), report.WorkStart);
    }

    [Fact]
    public async Task AnEmployeeWithNoRecordIsAbsentOnEveryWorkingDayThatIsOver()
    {
        var report = await ReportAsync([Anna, Marco], [Record(Anna, 1, 1, 8, 0), Record(Anna, 2, 2, 8, 0)]);

        var absent = Assert.Single(report.Absences);
        Assert.Equal(Marco.Id, absent.EmployeeId);
        // The weekend and today are left out.
        Assert.Equal([Day(1), Day(2)], absent.Dates);
    }

    [Fact]
    public async Task TodayIsNeverAnAbsence()
    {
        var report = await ReportAsync([Anna], [], from: Day(5), to: Day(5));

        Assert.Empty(report.Absences);
    }

    [Fact]
    public async Task AnOpenRecordCountsAsPresent()
    {
        var report = await ReportAsync([Anna], [Record(Anna, 1, 1, 8, 0, hours: null), Record(Anna, 2, 2, 8, 0)]);

        Assert.Empty(report.Absences);
    }

    [Fact]
    public async Task AnInactiveEmployeeIsNeverAbsent_AndOnlyAppearsInTheSummaryWithRecords()
    {
        var withoutRecords = await ReportAsync([Anna, Paolo], [Record(Anna, 1, 1, 8, 0)]);
        var withRecords = await ReportAsync([Anna, Paolo], [Record(Paolo, 1, 1, 8, 0)]);

        Assert.DoesNotContain(withoutRecords.Absences, absent => absent.EmployeeId == Paolo.Id);
        Assert.DoesNotContain(withoutRecords.Summary, row => row.EmployeeId == Paolo.Id);
        Assert.Contains(withRecords.Summary, row => row.EmployeeId == Paolo.Id);
        Assert.DoesNotContain(withRecords.Absences, absent => absent.EmployeeId == Paolo.Id);
    }

    [Fact]
    public async Task TheWorkingDaysComeFromTheOptions()
    {
        var options = new ReportOptions { WorkingDays = [DayOfWeek.Saturday] };

        var report = await ReportAsync([Anna], [], options: options);

        Assert.Equal([Day(3)], Assert.Single(report.Absences).Dates);
    }

    [Fact]
    public async Task TheSummaryCountsDaysHoursLateArrivalsAndAbsences()
    {
        var report = await ReportAsync(
            [Anna, Marco],
            [
                Record(Anna, 1, 1, 8, 0, hours: 8),
                Record(Anna, 2, 2, 9, 0, hours: 7.5),
                Record(Marco, 3, 1, 8, 0, hours: null)
            ]);

        var anna = report.Summary.Single(row => row.EmployeeId == Anna.Id);
        Assert.Equal(2, anna.DaysWorked);
        Assert.Equal(TimeSpan.FromHours(15.5), anna.TotalWorked);
        Assert.Equal(1, anna.LateCount);
        Assert.Equal(0, anna.AbsentCount);

        // Marco clocked in on the 1st and never clocked out: a day worked, but no hours.
        var marco = report.Summary.Single(row => row.EmployeeId == Marco.Id);
        Assert.Equal(1, marco.DaysWorked);
        Assert.Equal(TimeSpan.Zero, marco.TotalWorked);
        Assert.Equal(1, marco.AbsentCount);
    }

    [Fact]
    public async Task TheSummaryListsEveryActiveEmployeeByName()
    {
        var report = await ReportAsync([Marco, Anna], []);

        Assert.Equal(["Anna Cruz", "Marco Diaz"], report.Summary.Select(row => row.FullName));
    }

    [Fact]
    public async Task WithoutARange_ReturnsTheCurrentMonthUpToToday()
    {
        var result = await CreateService([Anna], []).GetReportAsync(null, null);

        Assert.Equal(Day(1), result.Report!.From);
        Assert.Equal(Day(5), result.Report.To);
    }

    [Fact]
    public async Task TheReportNamesTheTimeZoneThatDecidesTheDays()
    {
        var report = await ReportAsync([Anna], []);

        Assert.Equal("Test+8", report.TimeZoneId);
        Assert.Equal(10, report.GraceMinutes);
    }

    [Fact]
    public async Task ARangeLongerThanThirtyOneDays_IsInvalid()
    {
        var result = await CreateService([Anna], []).GetReportAsync(new DateOnly(2026, 8, 1), Day(5));

        Assert.Equal(ReportOutcome.Invalid, result.Outcome);
        Assert.Null(result.Report);
        Assert.Contains("To", result.Errors.Keys);
    }
}
