using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using FreeCodeSpot.Attendance.Application.Employees;
using FreeCodeSpot.Attendance.Application.Security;
using FreeCodeSpot.Attendance.Domain.Employees;
using FreeCodeSpot.Attendance.Infrastructure.Identity;
using FreeCodeSpot.Attendance.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreeCodeSpot.Attendance.IntegrationTests;

/// <summary>
/// Boots the API against its own SQL Server database. The database is rebuilt
/// from the migrations and seeded with the two demo users before the tests run,
/// and dropped afterwards, so the tests never touch the development data and
/// always see the seeded roster.
/// </summary>
public sealed class AttendanceApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string ManagerEmail = "manager@test.local";
    public const string EmployeeEmail = "employee@test.local";
    public const string Password = "Test#Passw0rd";

    private const string TestConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=FreeCodeSpotAttendance_Tests;Trusted_Connection=True;TrustServerCertificate=True";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not Development: the API seeds on startup there, and these tests seed after migrating.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:AttendanceDb", TestConnectionString);
        builder.UseSetting("SeedUsers:ManagerEmail", ManagerEmail);
        builder.UseSetting("SeedUsers:EmployeeEmail", EmployeeEmail);
        builder.UseSetting("SeedUsers:Password", Password);
        // The days of a timesheet are UTC days, so the tests do not depend on the machine.
        builder.UseSetting("Attendance:TimeZoneId", "UTC");
        // The seeded roster has Regie Baquero (id 1) and Anna Cruz (id 2).
        builder.UseSetting("SeedUsers:ManagerRosterEmail", "regie@freecodespot.local");
        builder.UseSetting("SeedUsers:EmployeeRosterEmail", "anna@freecodespot.local");
    }

    public async Task InitializeAsync()
    {
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AttendanceDbContext>();

            await db.Database.EnsureDeletedAsync();
            await db.Database.MigrateAsync();
        }

        await IdentitySeeder.SeedAsync(Services);
    }

    /// <summary>Signs in through /auth/login and returns a client that sends the bearer token.</summary>
    public async Task<HttpClient> CreateSignedInClientAsync(string email, string password = Password)
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login", new { Email = email, Password = password });
        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        return client;
    }

    /// <summary>
    /// Creates a new employee and a user in the Employee role linked to it, and returns a
    /// client signed in as that user. Each test that needs an employee gets its own.
    /// </summary>
    public async Task<(HttpClient Client, Employee Employee)> CreateLinkedEmployeeClientAsync()
    {
        using var scope = Services.CreateScope();
        var employees = scope.ServiceProvider.GetRequiredService<EmployeeService>();

        var email = $"{Guid.NewGuid():N}@test.local";
        var created = await employees.CreateAsync(new EmployeeDetails("Joy Mendoza", "Finance", email));
        var employee = created.Employee!;

        await CreateUserAsync(scope, email, employee.Id);
        return (await CreateSignedInClientAsync(email), employee);
    }

    /// <summary>Creates a user in the Employee role with no employee linked, and signs in as that user.</summary>
    public async Task<HttpClient> CreateUnlinkedUserClientAsync()
    {
        using var scope = Services.CreateScope();
        var email = $"{Guid.NewGuid():N}@test.local";

        await CreateUserAsync(scope, email, employeeId: null);
        return await CreateSignedInClientAsync(email);
    }

    private static async Task CreateUserAsync(IServiceScope scope, string email, int? employeeId)
    {
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };

        Check(await users.CreateAsync(user, Password));
        Check(await users.AddToRoleAsync(user, Roles.Employee));

        if (employeeId is not null)
        {
            Check(await users.AddClaimAsync(user, new Claim(AttendanceClaims.EmployeeId, employeeId.Value.ToString())));
        }
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AttendanceDbContext>();
            await db.Database.EnsureDeletedAsync();
        }

        await base.DisposeAsync();
    }

    private sealed record TokenResponse(string AccessToken);
}
