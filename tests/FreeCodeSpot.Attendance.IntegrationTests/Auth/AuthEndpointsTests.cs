using System.Net;
using System.Net.Http.Json;
using FreeCodeSpot.Attendance.Api.Auth;

namespace FreeCodeSpot.Attendance.IntegrationTests.Auth;

/// <summary>
/// Calls the sign-in routes and the protected employee routes as an anonymous
/// caller, as an employee and as a manager.
/// </summary>
[Collection(AttendanceApiCollection.Name)]
public class AuthEndpointsTests(AttendanceApiFactory factory)
{
    [Fact]
    public async Task Login_WithTheRightPassword_ReturnsABearerToken()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login", new { Email = AttendanceApiFactory.ManagerEmail, Password = AttendanceApiFactory.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("accessToken", body);
    }

    [Fact]
    public async Task Login_WithTheWrongPassword_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login", new { Email = AttendanceApiFactory.ManagerEmail, Password = "Wrong#Passw0rd" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithAnUnknownEmail_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login", new { Email = "nobody@test.local", Password = AttendanceApiFactory.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_ReturnsTheEmailAndRolesOfTheManager()
    {
        using var client = await factory.CreateSignedInClientAsync(AttendanceApiFactory.ManagerEmail);

        var me = await client.GetFromJsonAsync<CurrentUser>("/auth/me");

        Assert.Equal(AttendanceApiFactory.ManagerEmail, me!.Email);
        Assert.Equal(["Manager"], me.Roles);
    }

    [Fact]
    public async Task Me_ReturnsTheEmailAndRolesOfTheEmployee()
    {
        using var client = await factory.CreateSignedInClientAsync(AttendanceApiFactory.EmployeeEmail);

        var me = await client.GetFromJsonAsync<CurrentUser>("/auth/me");

        Assert.Equal(["Employee"], me!.Roles);
    }

    [Fact]
    public async Task Me_Returns401WithoutAToken()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Employees_Return401WithoutAToken()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/employees");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Employees_Return401WithATokenThatIsNotValid()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "not-a-real-token");

        var response = await client.GetAsync("/api/employees");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Employees_Return403ForAnEmployee()
    {
        using var client = await factory.CreateSignedInClientAsync(AttendanceApiFactory.EmployeeEmail);

        var read = await client.GetAsync("/api/employees");
        var write = await client.PostAsJsonAsync("/api/employees", new { FullName = "Joy Mendoza", Department = "Finance", Email = "joy@test.local" });

        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }

    [Fact]
    public async Task Employees_Return200ForAManager()
    {
        using var client = await factory.CreateSignedInClientAsync(AttendanceApiFactory.ManagerEmail);

        var response = await client.GetAsync("/api/employees");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
