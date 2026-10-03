using FreeCodeSpot.Attendance.Application.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FreeCodeSpot.Attendance.Infrastructure.Identity;

/// <summary>
/// Creates the two roles and the two demo users. Running it twice changes nothing.
/// </summary>
public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<SeedUsersOptions>>().Value;
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                Check(await roleManager.CreateAsync(new IdentityRole(role)));
            }
        }

        await EnsureUserAsync(userManager, options.ManagerEmail, options.Password, Roles.Manager);
        await EnsureUserAsync(userManager, options.EmployeeEmail, options.Password, Roles.Employee);
    }

    private static async Task EnsureUserAsync(
        UserManager<IdentityUser> userManager,
        string email,
        string password,
        string role)
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
