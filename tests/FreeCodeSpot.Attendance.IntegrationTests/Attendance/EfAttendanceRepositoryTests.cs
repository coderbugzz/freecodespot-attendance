using FreeCodeSpot.Attendance.Application.Attendance;
using FreeCodeSpot.Attendance.Application.Employees;
using FreeCodeSpot.Attendance.Domain.Attendance;
using Microsoft.Extensions.DependencyInjection;

namespace FreeCodeSpot.Attendance.IntegrationTests.Attendance;

/// <summary>
/// Runs the EF Core attendance repository against the migrated test database,
/// so the table, the foreign key and the one-open-record index are checked for real.
/// </summary>
[Collection(AttendanceApiCollection.Name)]
public sealed class EfAttendanceRepositoryTests : IDisposable
{
    private static readonly DateTimeOffset Morning = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);

    private readonly IServiceScope scope;
    private readonly IAttendanceRepository repository;
    private readonly IEmployeeRepository employees;

    public EfAttendanceRepositoryTests(AttendanceApiFactory factory)
    {
        scope = factory.Services.CreateScope();
        repository = scope.ServiceProvider.GetRequiredService<IAttendanceRepository>();
        employees = scope.ServiceProvider.GetRequiredService<IEmployeeRepository>();
    }

    public void Dispose() => scope.Dispose();

    [Fact]
    public async Task TryAdd_SavesTheRecordAndFindsItAsTheOpenRecord()
    {
        var employeeId = await NewEmployeeIdAsync();

        var added = await repository.TryAddAsync(new AttendanceRecord(0, employeeId, Morning));

        Assert.NotNull(added);
        Assert.True(added.Id > 0);
        var open = await repository.GetOpenRecordAsync(employeeId);
        Assert.Equal(Morning, open!.ClockInUtc);
    }

    [Fact]
    public async Task TryAdd_ReturnsNullWhenTheEmployeeAlreadyHasAnOpenRecord()
    {
        var employeeId = await NewEmployeeIdAsync();
        await repository.TryAddAsync(new AttendanceRecord(0, employeeId, Morning));

        var second = await repository.TryAddAsync(new AttendanceRecord(0, employeeId, Morning.AddMinutes(1)));

        Assert.Null(second);
    }

    [Fact]
    public async Task TryAdd_AllowsANewRecordOnceTheOpenOneIsClosed()
    {
        var employeeId = await NewEmployeeIdAsync();
        var first = await repository.TryAddAsync(new AttendanceRecord(0, employeeId, Morning));
        await repository.UpdateAsync(first! with { ClockOutUtc = Morning.AddHours(8) });

        var second = await repository.TryAddAsync(new AttendanceRecord(0, employeeId, Morning.AddDays(1)));

        Assert.NotNull(second);
        Assert.Null((await repository.GetOpenRecordAsync(employeeId))!.ClockOutUtc);
    }

    [Fact]
    public async Task Update_StoresTheClockOutTime()
    {
        var employeeId = await NewEmployeeIdAsync();
        var added = await repository.TryAddAsync(new AttendanceRecord(0, employeeId, Morning));

        await repository.UpdateAsync(added! with { ClockOutUtc = Morning.AddHours(8) });

        Assert.Null(await repository.GetOpenRecordAsync(employeeId));
        Assert.DoesNotContain(await repository.GetOpenRecordsAsync(), record => record.EmployeeId == employeeId);
    }

    private async Task<int> NewEmployeeIdAsync()
    {
        var email = $"{Guid.NewGuid():N}@test.local";
        var service = scope.ServiceProvider.GetRequiredService<EmployeeService>();
        var result = await service.CreateAsync(new EmployeeDetails("Joy Mendoza", "Finance", email));
        return result.Employee!.Id;
    }
}
