using System.Globalization;
using System.Security.Claims;
using FreeCodeSpot.Attendance.Application.Employees;
using FreeCodeSpot.Attendance.Application.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FreeCodeSpot.Attendance.Infrastructure.Identity;

/// <summary>
/// Creates the two roles and the two demo users, and links each user to the
/// employee record it belongs to. Running it twice changes nothing.
/// </summary>
public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<SeedUsersOptions>>().Value;
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var employees = scope.ServiceProvider.GetRequiredService<IEmployeeRepository>();

        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                Check(await roleManager.CreateAsync(new IdentityRole(role)));
            }
        }

        await EnsureUserAsync(userManager, employees, options.ManagerEmail, options.Password, Roles.Manager, options.ManagerRosterEmail);
        await EnsureUserAsync(userManager, employees, options.EmployeeEmail, options.Password, Roles.Employee, options.EmployeeRosterEmail);
    }

    private static async Task EnsureUserAsync(
        UserManager<IdentityUser> userManager,
        IEmployeeRepository employees,
        string email,
        string password,
        string role,
        string rosterEmail)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                $"SeedUsers is not configured for the {role} account. Set SeedUsers:Password and the account emails.");
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            Check(await userManager.CreateAsync(user, password));
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            Check(await userManager.AddToRoleAsync(user, role));
        }

        await LinkToEmployeeAsync(userManager, employees, user, rosterEmail);
    }

    /// <summary>
    /// Stores the id of the employee as a claim on the user. Identity puts the
    /// claims of a user in the bearer token, so the API knows whose attendance
    /// the caller may use without asking the request.
    /// </summary>
    private static async Task LinkToEmployeeAsync(
        UserManager<IdentityUser> userManager,
        IEmployeeRepository employees,
        IdentityUser user,
        string rosterEmail)
    {
        if (string.IsNullOrWhiteSpace(rosterEmail))
        {
            return;
        }

        var roster = await employees.GetAllAsync(includeInactive: true);
        var employee = roster.FirstOrDefault(e => string.Equals(e.Email, rosterEmail, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"No employee has the email {rosterEmail}. Check the SeedUsers settings.");

        var value = employee.Id.ToString(CultureInfo.InvariantCulture);
        var current = (await userManager.GetClaimsAsync(user))
            .Where(claim => claim.Type == AttendanceClaims.EmployeeId)
            .ToList();

        if (current.Count == 1 && current[0].Value == value)
        {
            return;
        }

        if (current.Count > 0)
        {
            Check(await userManager.RemoveClaimsAsync(user, current));
        }

        Check(await userManager.AddClaimAsync(user, new Claim(AttendanceClaims.EmployeeId, value)));
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Seeding identity data failed: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}
