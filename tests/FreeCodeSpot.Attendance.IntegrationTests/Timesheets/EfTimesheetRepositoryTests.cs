using FreeCodeSpot.Attendance.Application.Attendance;
using FreeCodeSpot.Attendance.Application.Employees;
using FreeCodeSpot.Attendance.Application.Timesheets;
using FreeCodeSpot.Attendance.Domain.Attendance;
using Microsoft.Extensions.DependencyInjection;

namespace FreeCodeSpot.Attendance.IntegrationTests.Timesheets;

/// <summary>
/// Runs the timesheet query against the migrated test database, so the date filter,
/// the join with the employees and the ordering are checked in SQL Server itself.
/// </summary>
[Collection(AttendanceApiCollection.Name)]
public sealed class EfTimesheetRepositoryTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 4, 6, 8, 0, 0, TimeSpan.Zero);

    private readonly IServiceScope scope;
    private readonly ITimesheetRepository timesheets;
    private readonly IAttendanceRepository attendance;

    public EfTimesheetRepositoryTests(AttendanceApiFactory factory)
    {
        scope = factory.Services.CreateScope();
        timesheets = scope.ServiceProvider.GetRequiredService<ITimesheetRepository>();
        attendance = scope.ServiceProvider.GetRequiredService<IAttendanceRepository>();
    }

    public void Dispose() => scope.Dispose();

    [Fact]
    public async Task ReturnsTheEmployeeNameAndDepartmentWithEachRecord()
    {
        var (id, name) = await NewEmployeeAsync();
        await attendance.TryAddAsync(new AttendanceRecord(0, id, Start, Start.AddHours(8)));

        var rows = await timesheets.GetRecordsAsync(Start.AddHours(-1), Start.AddHours(1), id);

        var row = Assert.Single(rows);
        Assert.Equal(name, row.FullName);
        Assert.Equal("Finance", row.Department);
        Assert.Equal(Start.AddHours(8), row.ClockOutUtc);
    }

    [Fact]
    public async Task TheStartOfTheRangeIsIncludedAndTheEndIsNot()
    {
        var (id, _) = await NewEmployeeAsync();
        await attendance.TryAddAsync(new AttendanceRecord(0, id, Start, Start.AddHours(1)));
        await attendance.TryAddAsync(new AttendanceRecord(0, id, Start.AddDays(1), Start.AddDays(1).AddHours(1)));

        var rows = await timesheets.GetRecordsAsync(Start, Start.AddDays(1), id);

        Assert.Equal(Start, Assert.Single(rows).ClockInUtc);
    }

    [Fact]
    public async Task WithoutAnEmployeeItReturnsEveryEmployeeOrderedByNameThenTime()
    {
        var (first, firstName) = await NewEmployeeAsync();
        var (second, secondName) = await NewEmployeeAsync();
        await attendance.TryAddAsync(new AttendanceRecord(0, second, Start, Start.AddHours(1)));
        await attendance.TryAddAsync(new AttendanceRecord(0, first, Start.AddHours(3), Start.AddHours(4)));
        await attendance.TryAddAsync(new AttendanceRecord(0, first, Start, Start.AddHours(1)));

        var rows = (await timesheets.GetRecordsAsync(Start.AddHours(-1), Start.AddDays(1), null))
            .Where(row => row.EmployeeId == first || row.EmployeeId == second)
            .ToList();

        var expectedOrder = new[] { (first, firstName), (second, secondName) }.OrderBy(pair => pair.Item2).Select(pair => pair.Item1).ToList();
        Assert.Equal(expectedOrder.Distinct(), rows.Select(row => row.EmployeeId).Distinct());
        var firstRows = rows.Where(row => row.EmployeeId == first).ToList();
        Assert.Equal([Start, Start.AddHours(3)], firstRows.Select(row => row.ClockInUtc));
    }

    [Fact]
    public async Task AnOpenRecordIsReturnedWithoutAClockOutTime()
    {
        var (id, _) = await NewEmployeeAsync();
        await attendance.TryAddAsync(new AttendanceRecord(0, id, Start));

        var rows = await timesheets.GetRecordsAsync(Start.AddHours(-1), Start.AddHours(1), id);

        Assert.Null(Assert.Single(rows).ClockOutUtc);
    }

    private async Task<(int Id, string Name)> NewEmployeeAsync()
    {
        var service = scope.ServiceProvider.GetRequiredService<EmployeeService>();
        var email = $"{Guid.NewGuid():N}@test.local";
        var name = $"Test {Guid.NewGuid():N}"[..20];
        var result = await service.CreateAsync(new EmployeeDetails(name, "Finance", email));
        return (result.Employee!.Id, name);
    }
}
