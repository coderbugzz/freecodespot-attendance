using System.Net;
using System.Net.Http.Json;
using FreeCodeSpot.Attendance.Domain.Employees;
using Microsoft.AspNetCore.Http;

namespace FreeCodeSpot.Attendance.IntegrationTests.Employees;

/// <summary>
/// Boots the real API in memory and calls it over HTTP, so routing, DI and
/// JSON serialization are all exercised together, against the test database.
/// </summary>
[Collection(AttendanceApiCollection.Name)]
public class EmployeeEndpointsTests(AttendanceApiFactory factory)
{
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task GetEmployees_ReturnsTheRosterOrderedByName()
    {
        var response = await client.GetAsync("/api/employees");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var employees = await response.Content.ReadFromJsonAsync<List<Employee>>();

        Assert.NotNull(employees);
        Assert.Contains(employees, employee => employee.FullName == "Marco Diaz");
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

    [Fact]
    public async Task CreateEmployee_Returns201WithTheLocationOfTheNewEmployee()
    {
        var email = NewEmail();

        var response = await client.PostAsJsonAsync("/api/employees", new { FullName = "Joy Mendoza", Department = "Finance", Email = email });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<Employee>();
        Assert.Equal(email, created!.Email);
        Assert.True(created.IsActive);
        Assert.Equal($"/api/employees/{created.Id}", response.Headers.Location!.AbsolutePath);
    }

    [Fact]
    public async Task CreateEmployee_Returns400WithTheFieldErrors()
    {
        var response = await client.PostAsJsonAsync("/api/employees", new { FullName = "", Department = "Finance", Email = "not-an-email" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Equal(["Name is required."], problem!.Errors["FullName"]);
        Assert.Equal(["Email is not a valid email address."], problem.Errors["Email"]);
    }

    [Fact]
    public async Task CreateEmployee_Returns400WhenTheEmailIsTaken()
    {
        var response = await client.PostAsJsonAsync("/api/employees", new { FullName = "Anna Two", Department = "Finance", Email = "Anna@FreeCodeSpot.local" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Equal(["Another employee already uses this email."], problem!.Errors["Email"]);
    }

    [Fact]
    public async Task UpdateEmployee_SavesTheChanges()
    {
        var created = await CreateAsync();

        var response = await client.PutAsJsonAsync($"/api/employees/{created.Id}", new { FullName = "Joy M. Mendoza", Department = "Operations", Email = created.Email });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reloaded = await client.GetFromJsonAsync<Employee>($"/api/employees/{created.Id}");
        Assert.Equal("Joy M. Mendoza", reloaded!.FullName);
        Assert.Equal("Operations", reloaded.Department);
    }

    [Fact]
    public async Task UpdateEmployee_Returns404ForUnknownId()
    {
        var response = await client.PutAsJsonAsync("/api/employees/999", new { FullName = "Nobody", Department = "None", Email = NewEmail() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Deactivate_HidesTheEmployeeFromTheRosterButKeepsTheRecord()
    {
        var created = await CreateAsync();

        var response = await client.PostAsync($"/api/employees/{created.Id}/deactivate", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var active = await client.GetFromJsonAsync<List<Employee>>("/api/employees");
        Assert.DoesNotContain(active!, employee => employee.Id == created.Id);

        var all = await client.GetFromJsonAsync<List<Employee>>("/api/employees?includeInactive=true");
        Assert.Contains(all!, employee => employee.Id == created.Id && !employee.IsActive);

        var byId = await client.GetFromJsonAsync<Employee>($"/api/employees/{created.Id}");
        Assert.False(byId!.IsActive);
    }

    [Fact]
    public async Task Activate_PutsTheEmployeeBackOnTheRoster()
    {
        var created = await CreateAsync();
        await client.PostAsync($"/api/employees/{created.Id}/deactivate", null);

        var response = await client.PostAsync($"/api/employees/{created.Id}/activate", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var active = await client.GetFromJsonAsync<List<Employee>>("/api/employees");
        Assert.Contains(active!, employee => employee.Id == created.Id);
    }

    [Fact]
    public async Task Deactivate_Returns404ForUnknownId()
    {
        var response = await client.PostAsync("/api/employees/999/deactivate", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static string NewEmail() => $"{Guid.NewGuid():N}@test.local";

    private async Task<Employee> CreateAsync()
    {
        var response = await client.PostAsJsonAsync("/api/employees", new { FullName = "Joy Mendoza", Department = "Finance", Email = NewEmail() });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Employee>())!;
    }
}
