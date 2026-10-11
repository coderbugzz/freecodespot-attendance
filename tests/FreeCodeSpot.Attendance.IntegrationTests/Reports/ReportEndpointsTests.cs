using System.Net;
using System.Net.Http.Json;
using FreeCodeSpot.Attendance.Application.Attendance;
using FreeCodeSpot.Attendance.Application.Reports;
using FreeCodeSpot.Attendance.Domain.Attendance;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FreeCodeSpot.Attendance.IntegrationTests.Reports;

/// <summary>
/// Calls the report route over HTTP. The test factory runs the API in UTC with the
/// default rules: work starts at 08:00, there are ten minutes of grace, and Monday to
/// Friday are working days. The records use fixed past days (Monday 2026-03-02 to
/// Friday 2026-03-06), so every day of the range is over and no test depends on today.
/// </summary>
[Collection(AttendanceApiCollection.Name)]
public class ReportEndpointsTests(AttendanceApiFactory factory)
{
    private static readonly DateTimeOffset Monday = new(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);

    private const string WeekRange = "from=2026-03-02&to=2026-03-06";

    [Fact]
    public async Task Report_ListsALateArrivalWithTheMinutesAfterTheStartOfWork()
    {
        using var manager = await factory.CreateSignedInClientAsync(AttendanceApiFactory.ManagerEmail);
        var (_, employee) = await factory.CreateLinkedEmployeeClientAsync();
        await SaveAsync(employee.Id, Monday.AddMinutes(25), Monday.AddHours(8));
        await SaveAsync(employee.Id, Monday.AddDays(1).AddMinutes(5), Monday.AddDays(1).AddHours(8));

        var report = await manager.GetFromJsonAsync<AttendanceReport>($"/api/reports?{WeekRange}");

        var late = Assert.Single(report!.LateArrivals, arrival => arrival.EmployeeId == employee.Id);
        Assert.Equal(new DateOnly(2026, 3, 2), late.Date);
        Assert.Equal(TimeSpan.FromMinutes(25), late.Late);
        Assert.Equal("UTC", report.TimeZoneId);
        Assert.Equal(new TimeOnly(8, 0), report.WorkStart);
        Assert.Equal(10, report.GraceMinutes);
    }

    [Fact]
    public async Task Report_ListsTheWorkingDaysAnEmployeeHasNoRecordOn()
    {
        using var manager = await factory.CreateSignedInClientAsync(AttendanceApiFactory.ManagerEmail);
        var (_, employee) = await factory.CreateLinkedEmployeeClientAsync();
        await SaveAsync(employee.Id, Monday, Monday.AddHours(8));
        await SaveAsync(employee.Id, Monday.AddDays(2), Monday.AddDays(2).AddHours(8));

        var report = await manager.GetFromJsonAsync<AttendanceReport>($"/api/reports?{WeekRange}");

        var absent = Assert.Single(report!.Absences, absences => absences.EmployeeId == employee.Id);
        Assert.Equal([new DateOnly(2026, 3, 3), new DateOnly(2026, 3, 5), new DateOnly(2026, 3, 6)], absent.Dates);
    }

    [Fact]
    public async Task Report_SummarisesTheDaysAndHoursOfAnEmployee()
    {
        using var manager = await factory.CreateSignedInClientAsync(AttendanceApiFactory.ManagerEmail);
        var (_, employee) = await factory.CreateLinkedEmployeeClientAsync();
        await SaveAsync(employee.Id, Monday, Monday.AddHours(8).AddMinutes(30));
        await SaveAsync(employee.Id, Monday.AddDays(1), Monday.AddDays(1).AddHours(4));

        var report = await manager.GetFromJsonAsync<AttendanceReport>($"/api/reports?{WeekRange}");

        var row = Assert.Single(report!.Summary, summary => summary.EmployeeId == employee.Id);
        Assert.Equal(2, row.DaysWorked);
        Assert.Equal(TimeSpan.FromHours(12).Add(TimeSpan.FromMinutes(30)), row.TotalWorked);
        Assert.Equal(0, row.LateCount);
        Assert.Equal(3, row.AbsentCount);
    }

    [Fact]
    public async Task Report_WithoutARangeCoversTheCurrentMonthUpToToday()
    {
        using var manager = await factory.CreateSignedInClientAsync(AttendanceApiFactory.ManagerEmail);

        var report = await manager.GetFromJsonAsync<AttendanceReport>("/api/reports");

        // Not compared with today's date, so the test cannot fail when it runs at midnight.
        Assert.Equal(1, report!.From.Day);
        Assert.Equal(report.From.Month, report.To.Month);
        Assert.True(report.To >= report.From);
    }

    [Fact]
    public async Task Report_Returns400ForARangeLongerThanThirtyOneDays()
    {
        using var manager = await factory.CreateSignedInClientAsync(AttendanceApiFactory.ManagerEmail);

        var response = await manager.GetAsync("/api/reports?from=2026-01-01&to=2026-03-01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Contains("To", problem!.Errors.Keys);
    }

    [Fact]
    public async Task Report_Returns403ForAnEmployee()
    {
        var (employee, _) = await factory.CreateLinkedEmployeeClientAsync();

        var response = await employee.GetAsync("/api/reports");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Report_Returns401WithoutAToken()
    {
        var response = await factory.CreateClient().GetAsync("/api/reports");

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
