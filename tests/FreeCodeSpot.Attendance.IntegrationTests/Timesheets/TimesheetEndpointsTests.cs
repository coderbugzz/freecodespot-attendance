using System.Net;
using System.Net.Http.Json;
using FreeCodeSpot.Attendance.Application.Attendance;
using FreeCodeSpot.Attendance.Application.Timesheets;
using FreeCodeSpot.Attendance.Domain.Attendance;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FreeCodeSpot.Attendance.IntegrationTests.Timesheets;

/// <summary>
/// Calls the timesheet routes over HTTP. The test factory runs the API in UTC, so the
/// days of these tests are UTC days. Every test creates its own employees, and the
/// records are saved with fixed past times, so no test depends on today's date.
/// </summary>
[Collection(AttendanceApiCollection.Name)]
public class TimesheetEndpointsTests(AttendanceApiFactory factory)
{
    private static readonly DateTimeOffset FirstDay = new(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Timesheet_ReturnsTheRecordsOfTheRangeWithTheirHours()
    {
        using var manager = await factory.CreateSignedInClientAsync(AttendanceApiFactory.ManagerEmail);
        var (_, employee) = await factory.CreateLinkedEmployeeClientAsync();
        await SaveAsync(employee.Id, FirstDay, FirstDay.AddHours(8).AddMinutes(30));
        await SaveAsync(employee.Id, FirstDay.AddDays(1), FirstDay.AddDays(1).AddHours(4));
        await SaveAsync(employee.Id, FirstDay.AddDays(10), FirstDay.AddDays(10).AddHours(8));

        var timesheet = await manager.GetFromJsonAsync<Timesheet>(
            $"/api/attendance/timesheet?from=2026-03-02&to=2026-03-03&employeeId={employee.Id}");

        var sheet = Assert.Single(timesheet!.Employees);
        Assert.Equal(employee.FullName, sheet.FullName);
        Assert.Equal([new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 3)], sheet.Days.Select(day => day.Date));
        Assert.Equal(TimeSpan.FromHours(8).Add(TimeSpan.FromMinutes(30)), sheet.Days[0].Worked);
        Assert.Equal(TimeSpan.FromHours(12).Add(TimeSpan.FromMinutes(30)), sheet.TotalWorked);
        Assert.Equal("UTC", timesheet.TimeZoneId);
    }

    [Fact]
    public async Task Timesheet_WithoutAnEmployeeIncludesEveryEmployeeWithRecords()
    {
        using var manager = await factory.CreateSignedInClientAsync(AttendanceApiFactory.ManagerEmail);
        var (_, first) = await factory.CreateLinkedEmployeeClientAsync();
        var (_, second) = await factory.CreateLinkedEmployeeClientAsync();
        await SaveAsync(first.Id, FirstDay, FirstDay.AddHours(8));
        await SaveAsync(second.Id, FirstDay, FirstDay.AddHours(6));

        var timesheet = await manager.GetFromJsonAsync<Timesheet>("/api/attendance/timesheet?from=2026-03-02&to=2026-03-02");

        Assert.Contains(timesheet!.Employees, sheet => sheet.EmployeeId == first.Id);
        Assert.Contains(timesheet.Employees, sheet => sheet.EmployeeId == second.Id);
    }

    [Fact]
    public async Task Timesheet_ForOneEmployeeLeavesTheOthersOut()
    {
        using var manager = await factory.CreateSignedInClientAsync(AttendanceApiFactory.ManagerEmail);
        var (_, first) = await factory.CreateLinkedEmployeeClientAsync();
        var (_, second) = await factory.CreateLinkedEmployeeClientAsync();
        await SaveAsync(first.Id, FirstDay, FirstDay.AddHours(8));
        await SaveAsync(second.Id, FirstDay, FirstDay.AddHours(6));

        var timesheet = await manager.GetFromJsonAsync<Timesheet>(
            $"/api/attendance/timesheet?from=2026-03-02&to=2026-03-02&employeeId={first.Id}");

        Assert.Equal(first.Id, Assert.Single(timesheet!.Employees).EmployeeId);
    }

    [Fact]
    public async Task Timesheet_WithoutARangeCoversTheLastSevenDays()
    {
        using var manager = await factory.CreateSignedInClientAsync(AttendanceApiFactory.ManagerEmail);

        var timesheet = await manager.GetFromJsonAsync<Timesheet>("/api/attendance/timesheet");

        Assert.Equal(6, timesheet!.To.DayNumber - timesheet.From.DayNumber);
    }

    [Fact]
    public async Task Timesheet_Returns400ForARangeThatEndsBeforeItStarts()
    {
        using var manager = await factory.CreateSignedInClientAsync(AttendanceApiFactory.ManagerEmail);

        var response = await manager.GetAsync("/api/attendance/timesheet?from=2026-03-05&to=2026-03-01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Equal(["The end date must not be before the start date."], problem!.Errors["To"]);
    }

    [Fact]
    public async Task Timesheet_Returns400ForARangeLongerThanThirtyOneDays()
    {
        using var manager = await factory.CreateSignedInClientAsync(AttendanceApiFactory.ManagerEmail);

        var response = await manager.GetAsync("/api/attendance/timesheet?from=2026-01-01&to=2026-03-01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Timesheet_Returns404ForAnUnknownEmployee()
    {
        using var manager = await factory.CreateSignedInClientAsync(AttendanceApiFactory.ManagerEmail);

        var response = await manager.GetAsync("/api/attendance/timesheet?employeeId=999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Timesheet_Returns403ForAnEmployee()
    {
        var (employee, _) = await factory.CreateLinkedEmployeeClientAsync();

        var response = await employee.GetAsync("/api/attendance/timesheet");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MyTimesheet_ShowsOnlyTheRecordsOfTheSignedInEmployee()
    {
        var (mine, me) = await factory.CreateLinkedEmployeeClientAsync();
        var (_, colleague) = await factory.CreateLinkedEmployeeClientAsync();
        await SaveAsync(me.Id, FirstDay, FirstDay.AddHours(8));
        await SaveAsync(colleague.Id, FirstDay, FirstDay.AddHours(6));

        var timesheet = await mine.GetFromJsonAsync<Timesheet>("/api/attendance/me/timesheet?from=2026-03-02&to=2026-03-02");

        var sheet = Assert.Single(timesheet!.Employees);
        Assert.Equal(me.Id, sheet.EmployeeId);
        Assert.Equal(TimeSpan.FromHours(8), sheet.TotalWorked);
    }

    [Fact]
    public async Task MyTimesheet_Returns403ForAUserWithoutALinkedEmployee()
    {
        using var client = await factory.CreateUnlinkedUserClientAsync();

        var response = await client.GetAsync("/api/attendance/me/timesheet");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MyTimesheet_Returns401WithoutAToken()
    {
        var response = await factory.CreateClient().GetAsync("/api/attendance/me/timesheet");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task SaveAsync(int employeeId, DateTimeOffset clockIn, DateTimeOffset? clockOut)
    {
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAttendanceRepository>();

        var saved = await repository.TryAddAsync(new AttendanceRecord(0, employeeId, clockIn, clockOut));
        Assert.NotNull(saved);
    }
}
