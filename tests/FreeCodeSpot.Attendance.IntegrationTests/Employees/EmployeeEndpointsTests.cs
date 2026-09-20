using System.Net;
using System.Net.Http.Json;
using FreeCodeSpot.Attendance.Domain.Employees;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FreeCodeSpot.Attendance.IntegrationTests.Employees;

/// <summary>
/// Boots the real API in memory and calls it over HTTP, so routing, DI and
/// JSON serialization are all exercised together.
/// </summary>
public class EmployeeEndpointsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task GetEmployees_ReturnsTheRosterOrderedByName()
    {
        var response = await client.GetAsync("/api/employees");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var employees = await response.Content.ReadFromJsonAsync<List<Employee>>();

        Assert.NotNull(employees);
        Assert.Equal(5, employees.Count);
        Assert.Equal(
            employees.Select(employee => employee.FullName).OrderBy(name => name, StringComparer.OrdinalIgnoreCase),
            employees.Select(employee => employee.FullName));
    }

    [Fact]
    public async Task GetEmployeeById_ReturnsTheEmployee()
    {
        var employee = await client.GetFromJsonAsync<Employee>("/api/employees/3");

        Assert.NotNull(employee);
        Assert.Equal("Marco Diaz", employee.FullName);
    }

    [Fact]
    public async Task GetEmployeeById_Returns404ForUnknownId()
    {
        var response = await client.GetAsync("/api/employees/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
