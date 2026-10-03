using System.Net.Http.Headers;
using System.Net.Http.Json;
using FreeCodeSpot.Attendance.Infrastructure.Identity;
using FreeCodeSpot.Attendance.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
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
