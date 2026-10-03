using FreeCodeSpot.Attendance.Application.Attendance;
using FreeCodeSpot.Attendance.Application.Employees;
using FreeCodeSpot.Attendance.Domain.Attendance;
using FreeCodeSpot.Attendance.Domain.Employees;

namespace FreeCodeSpot.Attendance.UnitTests.Attendance;

public class AttendanceServiceTests
{
    /// <summary>A clock the test controls, so the recorded times are known.</summary>
    private sealed class FakeClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    /// <summary>A list-backed roster. Only the lookups the attendance rules use do real work.</summary>
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

    /// <summary>
    /// A list-backed attendance store. RefuseInserts makes TryAddAsync behave like the
    /// database does when another request has just clocked the employee in.
    /// </summary>
    private sealed class FakeAttendance : IAttendanceRepository
    {
        public List<AttendanceRecord> Records { get; } = [];

        public bool RefuseInserts { get; set; }

        public Task<IReadOnlyList<AttendanceRecord>> GetOpenRecordsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AttendanceRecord>>(Records.Where(record => record.IsOpen).ToList());

        public Task<AttendanceRecord?> GetOpenRecordAsync(int employeeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Records.FirstOrDefault(record => record.EmployeeId == employeeId && record.IsOpen));

        public Task<IReadOnlyList<AttendanceRecord>> GetRecentRecordsAsync(int employeeId, int count, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AttendanceRecord>>(Records
                .Where(record => record.EmployeeId == employeeId)
                .OrderByDescending(record => record.ClockInUtc)
                .Take(count)
                .ToList());

        public Task<AttendanceRecord?> TryAddAsync(AttendanceRecord record, CancellationToken cancellationToken = default)
        {
            if (RefuseInserts)
            {
                return Task.FromResult<AttendanceRecord?>(null);
            }

            var added = record with { Id = Records.Count + 1 };
            Records.Add(added);
            return Task.FromResult<AttendanceRecord?>(added);
        }

        public Task UpdateAsync(AttendanceRecord record, CancellationToken cancellationToken = default)
        {
            Records[Records.FindIndex(existing => existing.Id == record.Id)] = record;
            return Task.CompletedTask;
        }
    }

    private static readonly Employee Anna = new(1, "Anna Cruz", "Engineering", "anna@example.com");
    private static readonly Employee Marco = new(2, "Marco Diaz", "Support", "marco@example.com");
    private static readonly Employee Paolo = new(3, "Paolo Santos", "Operations", "paolo@example.com", IsActive: false);

    private static readonly DateTimeOffset Morning = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeClock clock = new(Morning);
    private readonly FakeAttendance store = new();

    private AttendanceService CreateService(params Employee[] employees) =>
        new(store, new FakeEmployees(employees), clock);

    [Fact]
    public async Task ClockIn_RecordsTheCurrentTimeAsAnOpenRecord()
    {
        var result = await CreateService(Anna).ClockInAsync(Anna.Id);

        Assert.Equal(AttendanceOutcome.Success, result.Outcome);
        Assert.Equal(Anna.Id, result.Record!.EmployeeId);
        Assert.Equal(Morning, result.Record.ClockInUtc);
        Assert.True(result.Record.IsOpen);
    }

    [Fact]
    public async Task ClockIn_IsRefusedWhenTheEmployeeIsAlreadyClockedIn()
    {
        var service = CreateService(Anna);
        await service.ClockInAsync(Anna.Id);

        var result = await service.ClockInAsync(Anna.Id);

        Assert.Equal(AttendanceOutcome.Conflict, result.Outcome);
        Assert.Equal("Anna Cruz is already clocked in.", result.Message);
        Assert.Single(store.Records);
    }

    [Fact]
    public async Task ClockIn_IsRefusedWhenTheDatabaseFindsAnotherOpenRecord()
    {
        store.RefuseInserts = true;

        var result = await CreateService(Anna).ClockInAsync(Anna.Id);

        Assert.Equal(AttendanceOutcome.Conflict, result.Outcome);
        Assert.Equal("Anna Cruz is already clocked in.", result.Message);
    }

    [Fact]
    public async Task ClockIn_IsRefusedForAnInactiveEmployee()
    {
        var result = await CreateService(Paolo).ClockInAsync(Paolo.Id);

        Assert.Equal(AttendanceOutcome.Conflict, result.Outcome);
        Assert.Equal("Paolo Santos is inactive and cannot clock in.", result.Message);
        Assert.Empty(store.Records);
    }

    [Fact]
    public async Task ClockIn_ReturnsNotFoundForAnUnknownEmployee()
    {
        var result = await CreateService(Anna).ClockInAsync(99);

        Assert.Equal(AttendanceOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task ClockIn_StartsANewRecordAfterTheEmployeeClockedOut()
    {
        var service = CreateService(Anna);
        await service.ClockInAsync(Anna.Id);
        await service.ClockOutAsync(Anna.Id);

        var result = await service.ClockInAsync(Anna.Id);

        Assert.Equal(AttendanceOutcome.Success, result.Outcome);
        Assert.Equal(2, store.Records.Count);
    }

    [Fact]
    public async Task ClockOut_ClosesTheOpenRecordAtTheCurrentTime()
    {
        var service = CreateService(Anna);
        await service.ClockInAsync(Anna.Id);
        clock.Now = Morning.AddHours(8).AddMinutes(30);

        var result = await service.ClockOutAsync(Anna.Id);

        Assert.Equal(AttendanceOutcome.Success, result.Outcome);
        Assert.Equal(Morning, result.Record!.ClockInUtc);
        Assert.Equal(Morning.AddHours(8).AddMinutes(30), result.Record.ClockOutUtc);
        Assert.False(result.Record.IsOpen);
    }

    [Fact]
    public async Task ClockOut_IsRefusedWhenTheEmployeeNeverClockedIn()
    {
        var result = await CreateService(Anna).ClockOutAsync(Anna.Id);

        Assert.Equal(AttendanceOutcome.Conflict, result.Outcome);
        Assert.Equal("Anna Cruz is not clocked in.", result.Message);
    }

    [Fact]
    public async Task ClockOut_IsRefusedWhenTheRecordIsAlreadyClosed()
    {
        var service = CreateService(Anna);
        await service.ClockInAsync(Anna.Id);
        await service.ClockOutAsync(Anna.Id);
        var closedAt = store.Records.Single().ClockOutUtc;
        clock.Now = Morning.AddHours(9);

        var result = await service.ClockOutAsync(Anna.Id);

        Assert.Equal(AttendanceOutcome.Conflict, result.Outcome);
        Assert.Equal(closedAt, store.Records.Single().ClockOutUtc);
    }

    [Fact]
    public async Task ClockOut_ReturnsNotFoundForAnUnknownEmployee()
    {
        var result = await CreateService(Anna).ClockOutAsync(99);

        Assert.Equal(AttendanceOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task ClockOut_IsAllowedForAnEmployeeDeactivatedWhileClockedIn()
    {
        store.Records.Add(new AttendanceRecord(1, Paolo.Id, Morning));

        var result = await CreateService(Paolo).ClockOutAsync(Paolo.Id);

        Assert.Equal(AttendanceOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task GetStatus_ListsActiveEmployeesByNameWithTheirClockInTime()
    {
        store.Records.Add(new AttendanceRecord(1, Marco.Id, Morning));

        var status = await CreateService(Marco, Anna, Paolo).GetStatusAsync();

        Assert.Equal(["Anna Cruz", "Marco Diaz"], status.Select(item => item.FullName));
        Assert.Null(status[0].ClockedInSinceUtc);
        Assert.Equal(Morning, status[1].ClockedInSinceUtc);
    }

    [Fact]
    public async Task GetStatus_KeepsAnInactiveEmployeeWhoIsStillClockedIn()
    {
        store.Records.Add(new AttendanceRecord(1, Paolo.Id, Morning));

        var status = await CreateService(Anna, Paolo).GetStatusAsync();

        Assert.Equal(["Anna Cruz", "Paolo Santos"], status.Select(item => item.FullName));
    }

    [Fact]
    public async Task GetStatus_IgnoresClosedRecords()
    {
        store.Records.Add(new AttendanceRecord(1, Anna.Id, Morning, Morning.AddHours(8)));

        var status = await CreateService(Anna).GetStatusAsync();

        Assert.Null(status.Single().ClockedInSinceUtc);
    }

    [Fact]
    public async Task GetMyAttendance_ShowsOnlyTheRecordsOfThatEmployee()
    {
        store.Records.Add(new AttendanceRecord(1, Anna.Id, Morning, Morning.AddHours(8)));
        store.Records.Add(new AttendanceRecord(2, Marco.Id, Morning, Morning.AddHours(7)));

        var mine = await CreateService(Anna, Marco).GetMyAttendanceAsync(Anna.Id);

        Assert.Equal("Anna Cruz", mine!.Status.FullName);
        Assert.Equal([Anna.Id], mine.RecentRecords.Select(record => record.EmployeeId));
    }

    [Fact]
    public async Task GetMyAttendance_ShowsTheClockInTimeWhenClockedIn()
    {
        store.Records.Add(new AttendanceRecord(1, Anna.Id, Morning));

        var mine = await CreateService(Anna).GetMyAttendanceAsync(Anna.Id);

        Assert.Equal(Morning, mine!.Status.ClockedInSinceUtc);
    }

    [Fact]
    public async Task GetMyAttendance_IsNotClockedInWhenEveryRecordIsClosed()
    {
        store.Records.Add(new AttendanceRecord(1, Anna.Id, Morning, Morning.AddHours(8)));

        var mine = await CreateService(Anna).GetMyAttendanceAsync(Anna.Id);

        Assert.Null(mine!.Status.ClockedInSinceUtc);
        Assert.Single(mine.RecentRecords);
    }

    [Fact]
    public async Task GetMyAttendance_ReturnsTheLatestRecordsFirstAndNoMoreThanAsked()
    {
        for (var day = 0; day < 4; day++)
        {
            store.Records.Add(new AttendanceRecord(day + 1, Anna.Id, Morning.AddDays(day), Morning.AddDays(day).AddHours(8)));
        }

        var mine = await CreateService(Anna).GetMyAttendanceAsync(Anna.Id, recentCount: 3);

        Assert.Equal([Morning.AddDays(3), Morning.AddDays(2), Morning.AddDays(1)], mine!.RecentRecords.Select(record => record.ClockInUtc));
    }

    [Fact]
    public async Task GetMyAttendance_ReturnsNullForAnUnknownEmployee()
    {
        var mine = await CreateService(Anna).GetMyAttendanceAsync(99);

        Assert.Null(mine);
    }
}
