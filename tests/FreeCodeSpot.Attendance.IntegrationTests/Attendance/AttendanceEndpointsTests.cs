using System.Net;
using System.Net.Http.Json;
using FreeCodeSpot.Attendance.Application.Attendance;
using FreeCodeSpot.Attendance.Domain.Attendance;
using FreeCodeSpot.Attendance.Domain.Employees;
using Microsoft.AspNetCore.Mvc;

namespace FreeCodeSpot.Attendance.IntegrationTests.Attendance;

/// <summary>
/// Calls the attendance routes over HTTP against the test database. Every test
/// creates its own employee, so the clock-in state of one test never leaks into another.
/// </summary>
[Collection(AttendanceApiCollection.Name)]
public class AttendanceEndpointsTests(AttendanceApiFactory factory) : IAsyncLifetime
{
    private HttpClient client = null!;

    public async Task InitializeAsync() =>
        client = await factory.CreateSignedInClientAsync(AttendanceApiFactory.ManagerEmail);

    public Task DisposeAsync()
    {
        client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ClockIn_Returns200WithAnOpenRecord()
    {
        var employee = await CreateEmployeeAsync();

        var response = await client.PostAsync($"/api/attendance/{employee.Id}/clock-in", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var record = await response.Content.ReadFromJsonAsync<AttendanceRecord>();
        Assert.Equal(employee.Id, record!.EmployeeId);
        Assert.Null(record.ClockOutUtc);
        Assert.InRange(record.ClockInUtc, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task ClockIn_Returns409WhenTheEmployeeIsAlreadyClockedIn()
    {
        var employee = await CreateEmployeeAsync();
        await client.PostAsync($"/api/attendance/{employee.Id}/clock-in", null);

        var response = await client.PostAsync($"/api/attendance/{employee.Id}/clock-in", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal($"{employee.FullName} is already clocked in.", problem!.Detail);
    }

    [Fact]
    public async Task ClockIn_Returns409ForAnInactiveEmployee()
    {
        var employee = await CreateEmployeeAsync();
        await client.PostAsync($"/api/employees/{employee.Id}/deactivate", null);

        var response = await client.PostAsync($"/api/attendance/{employee.Id}/clock-in", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ClockIn_Returns404ForAnUnknownEmployee()
    {
        var response = await client.PostAsync("/api/attendance/999999/clock-in", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ClockOut_ClosesTheRecord()
    {
        var employee = await CreateEmployeeAsync();
        await client.PostAsync($"/api/attendance/{employee.Id}/clock-in", null);

        var response = await client.PostAsync($"/api/attendance/{employee.Id}/clock-out", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var record = await response.Content.ReadFromJsonAsync<AttendanceRecord>();
        Assert.NotNull(record!.ClockOutUtc);
        Assert.True(record.ClockOutUtc >= record.ClockInUtc);
    }

    [Fact]
    public async Task ClockOut_Returns409WhenTheEmployeeIsNotClockedIn()
    {
        var employee = await CreateEmployeeAsync();

        var response = await client.PostAsync($"/api/attendance/{employee.Id}/clock-out", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal($"{employee.FullName} is not clocked in.", problem!.Detail);
    }

    [Fact]
    public async Task ClockOut_Returns409WhenTheRecordIsAlreadyClosed()
    {
        var employee = await CreateEmployeeAsync();
        await client.PostAsync($"/api/attendance/{employee.Id}/clock-in", null);
        await client.PostAsync($"/api/attendance/{employee.Id}/clock-out", null);

        var response = await client.PostAsync($"/api/attendance/{employee.Id}/clock-out", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetStatus_ShowsWhoIsClockedIn()
    {
        var clockedIn = await CreateEmployeeAsync();
        var notClockedIn = await CreateEmployeeAsync();
        await client.PostAsync($"/api/attendance/{clockedIn.Id}/clock-in", null);

        var status = (await client.GetFromJsonAsync<List<EmployeeAttendanceStatus>>("/api/attendance"))!;

        Assert.NotNull(status.Single(item => item.EmployeeId == clockedIn.Id).ClockedInSinceUtc);
        Assert.Null(status.Single(item => item.EmployeeId == notClockedIn.Id).ClockedInSinceUtc);
    }

    [Fact]
    public async Task Attendance_Returns401WithoutAToken()
    {
        var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync("/api/attendance");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Attendance_Returns403ForAnEmployee()
    {
        var employee = await CreateEmployeeAsync();
        using var employeeClient = await factory.CreateSignedInClientAsync(AttendanceApiFactory.EmployeeEmail);

        var read = await employeeClient.GetAsync("/api/attendance");
        var clockIn = await employeeClient.PostAsync($"/api/attendance/{employee.Id}/clock-in", null);

        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, clockIn.StatusCode);
    }

    private async Task<Employee> CreateEmployeeAsync()
    {
        var email = $"{Guid.NewGuid():N}@test.local";
        var response = await client.PostAsJsonAsync("/api/employees", new { FullName = "Joy Mendoza", Department = "Finance", Email = email });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Employee>())!;
    }
}
