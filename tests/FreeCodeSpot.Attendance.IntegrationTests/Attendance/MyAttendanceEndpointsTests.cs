using System.Net;
using System.Net.Http.Json;
using FreeCodeSpot.Attendance.Application.Attendance;
using FreeCodeSpot.Attendance.Domain.Attendance;
using Microsoft.AspNetCore.Mvc;

namespace FreeCodeSpot.Attendance.IntegrationTests.Attendance;

/// <summary>
/// Calls the routes a signed-in user uses for their own attendance. Every test
/// creates its own employee and its own user, so one test never changes what
/// another sees.
/// </summary>
[Collection(AttendanceApiCollection.Name)]
public class MyAttendanceEndpointsTests(AttendanceApiFactory factory)
{
    [Fact]
    public async Task GetMine_ShowsTheEmployeeTheUserIsLinkedTo()
    {
        var (client, employee) = await factory.CreateLinkedEmployeeClientAsync();

        var mine = await client.GetFromJsonAsync<MyAttendance>("/api/attendance/me");

        Assert.Equal(employee.Id, mine!.Status.EmployeeId);
        Assert.Null(mine.Status.ClockedInSinceUtc);
        Assert.Empty(mine.RecentRecords);
    }

    [Fact]
    public async Task ClockInAsMyself_ShowsTheEmployeeAsClockedInWithAnOpenRecord()
    {
        var (client, employee) = await factory.CreateLinkedEmployeeClientAsync();

        var response = await client.PostAsync("/api/attendance/me/clock-in", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var mine = await client.GetFromJsonAsync<MyAttendance>("/api/attendance/me");
        Assert.NotNull(mine!.Status.ClockedInSinceUtc);
        var record = Assert.Single(mine.RecentRecords);
        Assert.Equal(employee.Id, record.EmployeeId);
        Assert.Null(record.ClockOutUtc);
    }

    [Fact]
    public async Task ClockOutAsMyself_ClosesTheRecord()
    {
        var (client, _) = await factory.CreateLinkedEmployeeClientAsync();
        await client.PostAsync("/api/attendance/me/clock-in", null);

        var response = await client.PostAsync("/api/attendance/me/clock-out", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var record = await response.Content.ReadFromJsonAsync<AttendanceRecord>();
        Assert.NotNull(record!.ClockOutUtc);
    }

    [Fact]
    public async Task ClockInAsMyself_Returns409WhenAlreadyClockedIn()
    {
        var (client, employee) = await factory.CreateLinkedEmployeeClientAsync();
        await client.PostAsync("/api/attendance/me/clock-in", null);

        var response = await client.PostAsync("/api/attendance/me/clock-in", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal($"{employee.FullName} is already clocked in.", problem!.Detail);
    }

    [Fact]
    public async Task EachUserOnlySeesTheirOwnAttendance()
    {
        var (first, _) = await factory.CreateLinkedEmployeeClientAsync();
        var (second, _) = await factory.CreateLinkedEmployeeClientAsync();
        await first.PostAsync("/api/attendance/me/clock-in", null);

        var mine = await second.GetFromJsonAsync<MyAttendance>("/api/attendance/me");

        Assert.Null(mine!.Status.ClockedInSinceUtc);
        Assert.Empty(mine.RecentRecords);
    }

    [Fact]
    public async Task AnEmployeeCannotClockInAColleague()
    {
        var (client, _) = await factory.CreateLinkedEmployeeClientAsync();
        var (_, colleague) = await factory.CreateLinkedEmployeeClientAsync();

        var response = await client.PostAsync($"/api/attendance/{colleague.Id}/clock-in", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var manager = await factory.CreateSignedInClientAsync(AttendanceApiFactory.ManagerEmail);
        var status = (await manager.GetFromJsonAsync<List<EmployeeAttendanceStatus>>("/api/attendance"))!;
        Assert.Null(status.Single(item => item.EmployeeId == colleague.Id).ClockedInSinceUtc);
    }

    [Fact]
    public async Task AnEmployeeCannotSeeTheAttendanceOfEveryone()
    {
        var (client, _) = await factory.CreateLinkedEmployeeClientAsync();

        var response = await client.GetAsync("/api/attendance");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AUserWithoutALinkedEmployeeGets403()
    {
        using var client = await factory.CreateUnlinkedUserClientAsync();

        var read = await client.GetAsync("/api/attendance/me");
        var clockIn = await client.PostAsync("/api/attendance/me/clock-in", null);

        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, clockIn.StatusCode);
    }

    [Fact]
    public async Task AManagerLinkedToAnEmployeeCanUseTheirOwnAttendance()
    {
        using var client = await factory.CreateSignedInClientAsync(AttendanceApiFactory.ManagerEmail);

        var mine = await client.GetFromJsonAsync<MyAttendance>("/api/attendance/me");

        Assert.Equal("Regie Baquero", mine!.Status.FullName);
    }

    [Fact]
    public async Task MyAttendance_Returns401WithoutAToken()
    {
        var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync("/api/attendance/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
