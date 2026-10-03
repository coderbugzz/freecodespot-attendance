using FreeCodeSpot.Attendance.Application.Employees;
using FreeCodeSpot.Attendance.Application.Timesheets;
using FreeCodeSpot.Attendance.Domain.Employees;

namespace FreeCodeSpot.Attendance.UnitTests.Timesheets;

public class TimesheetServiceTests
{
    private sealed class FakeClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>Only the lookup the timesheet rules use does real work.</summary>
    private sealed class FakeEmployees(params Employee[] seed) : IEmployeeRepository
    {
        public Task<IReadOnlyList<Employee>> GetAllAsync(bool includeInactive, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Employee?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(seed.FirstOrDefault(employee => employee.Id == id));

        public Task<bool> EmailExistsAsync(string email, int? exceptId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Employee> AddAsync(Employee employee, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UpdateAsync(Employee employee, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// A list-backed source that filters and orders like the SQL query does, so the
    /// service gets the same kind of rows from the fake as from the database.
    /// </summary>
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

    // A time zone eight hours ahead of UTC, so a day starts at 16:00 UTC the evening before.
    private static readonly TimeZoneInfo Plus8 =
        TimeZoneInfo.CreateCustomTimeZone("Test+8", TimeSpan.FromHours(8), "Test+8", "Test+8");

    private static readonly Employee Anna = new(1, "Anna Cruz", "Engineering", "anna@example.com");
    private static readonly Employee Marco = new(2, "Marco Diaz", "Support", "marco@example.com");

    // 2026-10-05 18:00 in Plus8.
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 10, 0, 0, TimeSpan.Zero);

    private static TimesheetRecord Record(Employee employee, int id, DateTimeOffset clockIn, DateTimeOffset? clockOut) =>
        new(employee.Id, employee.FullName, employee.Department, id, clockIn, clockOut);

    private static TimesheetService CreateService(params TimesheetRecord[] records) =>
        new(new FakeTimesheets(records), new FakeEmployees(Anna, Marco), new FakeClock(Now), Plus8);

    private static DateOnly Day(int day) => new(2026, 10, day);

    [Fact]
    public async Task WithoutARange_ReturnsTheLastSevenDaysIncludingToday()
    {
        var result = await CreateService().GetTimesheetAsync(null, null, null);

        Assert.Equal(TimesheetOutcome.Success, result.Outcome);
        Assert.Equal(new DateOnly(2026, 9, 29), result.Timesheet!.From);
        Assert.Equal(Day(5), result.Timesheet.To);
    }

    [Fact]
    public async Task TheTimesheetNamesTheTimeZoneThatDecidesTheDays()
    {
        var result = await CreateService().GetTimesheetAsync(Day(1), Day(5), null);

        Assert.Equal("Test+8", result.Timesheet!.TimeZoneId);
    }

    [Fact]
    public async Task ARecordLandsOnItsDayInTheTimeZoneNotInUtc()
    {
        // 17:00 UTC on the 4th is 01:00 on the 5th in Plus8.
        var clockIn = new DateTimeOffset(2026, 10, 4, 17, 0, 0, TimeSpan.Zero);
        var service = CreateService(Record(Anna, 1, clockIn, clockIn.AddHours(8)));

        var result = await service.GetTimesheetAsync(Day(4), Day(5), Anna.Id);

        var day = Assert.Single(Assert.Single(result.Timesheet!.Employees).Days);
        Assert.Equal(Day(5), day.Date);
    }

    [Fact]
    public async Task ARecordThatRunsPastMidnightCountsOnTheDayItWasClockedIn()
    {
        // 22:00 on the 4th to 06:00 on the 5th in Plus8.
        var clockIn = new DateTimeOffset(2026, 10, 4, 14, 0, 0, TimeSpan.Zero);
        var service = CreateService(Record(Anna, 1, clockIn, clockIn.AddHours(8)));

        var result = await service.GetTimesheetAsync(Day(4), Day(5), Anna.Id);

        var day = Assert.Single(Assert.Single(result.Timesheet!.Employees).Days);
        Assert.Equal(Day(4), day.Date);
        Assert.Equal(TimeSpan.FromHours(8), day.Worked);
    }

    [Fact]
    public async Task AddsUpTheClosedRecordsOfADayAndOfTheRange()
    {
        var morning = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
        var service = CreateService(
            Record(Anna, 1, morning, morning.AddHours(4)),
            Record(Anna, 2, morning.AddHours(5), morning.AddHours(8).AddMinutes(30)),
            Record(Anna, 3, morning.AddDays(-1), morning.AddDays(-1).AddHours(7)));

        var result = await service.GetTimesheetAsync(Day(3), Day(5), Anna.Id);

        var anna = Assert.Single(result.Timesheet!.Employees);
        Assert.Equal([Day(4), Day(5)], anna.Days.Select(day => day.Date));
        Assert.Equal(TimeSpan.FromHours(7), anna.Days[0].Worked);
        Assert.Equal(TimeSpan.FromHours(7).Add(TimeSpan.FromMinutes(30)), anna.Days[1].Worked);
        Assert.Equal(TimeSpan.FromHours(14).Add(TimeSpan.FromMinutes(30)), anna.TotalWorked);
    }

    [Fact]
    public async Task AnOpenRecordIsListedButAddsNothingToTheTotals()
    {
        var clockIn = new DateTimeOffset(2026, 10, 5, 1, 0, 0, TimeSpan.Zero);
        var service = CreateService(Record(Anna, 1, clockIn, null));

        var result = await service.GetTimesheetAsync(Day(5), Day(5), Anna.Id);

        var anna = Assert.Single(result.Timesheet!.Employees);
        var entry = Assert.Single(Assert.Single(anna.Days).Entries);
        Assert.Null(entry.ClockOutUtc);
        Assert.Null(entry.Duration);
        Assert.Equal(TimeSpan.Zero, anna.TotalWorked);
    }

    [Fact]
    public async Task EmployeesAreOrderedByNameAndOnlyThoseWithRecordsAreListed()
    {
        var clockIn = new DateTimeOffset(2026, 10, 5, 1, 0, 0, TimeSpan.Zero);
        var service = CreateService(
            Record(Marco, 2, clockIn, clockIn.AddHours(8)),
            Record(Anna, 1, clockIn, clockIn.AddHours(8)));

        var result = await service.GetTimesheetAsync(Day(5), Day(5), null);

        Assert.Equal(["Anna Cruz", "Marco Diaz"], result.Timesheet!.Employees.Select(employee => employee.FullName));
    }

    [Fact]
    public async Task AskingForOneEmployeeLeavesTheOthersOut()
    {
        var clockIn = new DateTimeOffset(2026, 10, 5, 1, 0, 0, TimeSpan.Zero);
        var service = CreateService(
            Record(Anna, 1, clockIn, clockIn.AddHours(8)),
            Record(Marco, 2, clockIn, clockIn.AddHours(8)));

        var result = await service.GetTimesheetAsync(Day(5), Day(5), Marco.Id);

        Assert.Equal("Marco Diaz", Assert.Single(result.Timesheet!.Employees).FullName);
    }

    [Fact]
    public async Task TheLastDayOfTheRangeIncludesTheWholeLocalDay()
    {
        // 16:30 UTC on the 5th is 00:30 on the 6th in Plus8.
        var clockIn = new DateTimeOffset(2026, 10, 5, 16, 30, 0, TimeSpan.Zero);
        var service = CreateService(Record(Anna, 1, clockIn, clockIn.AddHours(1)));

        var untilTheFifth = await service.GetTimesheetAsync(Day(1), Day(5), null);
        var untilTheSixth = await service.GetTimesheetAsync(Day(1), Day(6), null);

        Assert.Empty(untilTheFifth.Timesheet!.Employees);
        Assert.Single(untilTheSixth.Timesheet!.Employees);
    }

    [Fact]
    public async Task AnEndDateBeforeTheStartDateIsInvalid()
    {
        var result = await CreateService().GetTimesheetAsync(Day(5), Day(3), null);

        Assert.Equal(TimesheetOutcome.Invalid, result.Outcome);
        Assert.Equal(["The end date must not be before the start date."], result.Errors["To"]);
    }

    [Fact]
    public async Task ARangeLongerThanThirtyOneDaysIsInvalid()
    {
        var result = await CreateService().GetTimesheetAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 9, 1), null);

        Assert.Equal(TimesheetOutcome.Invalid, result.Outcome);
        Assert.Equal(["The range can be at most 31 days."], result.Errors["To"]);
    }

    [Fact]
    public async Task ARangeOfExactlyThirtyOneDaysIsAllowed()
    {
        var result = await CreateService().GetTimesheetAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), null);

        Assert.Equal(TimesheetOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task AnUnknownEmployeeIsNotFound()
    {
        var result = await CreateService().GetTimesheetAsync(Day(1), Day(5), 99);

        Assert.Equal(TimesheetOutcome.NotFound, result.Outcome);
    }
}
