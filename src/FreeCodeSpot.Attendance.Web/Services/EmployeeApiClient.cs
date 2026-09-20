using System.Net.Http.Json;
using FreeCodeSpot.Attendance.Web.Models;

namespace FreeCodeSpot.Attendance.Web.Services;

/// <summary>
/// Calls the attendance API. Components depend on this instead of holding an
/// HttpClient and a route string of their own.
/// </summary>
public sealed class EmployeeApiClient(HttpClient httpClient, ILogger<EmployeeApiClient> logger)
{
    public async Task<IReadOnlyList<Employee>> GetEmployeesAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Requesting the employee roster from {BaseAddress}", httpClient.BaseAddress);

        var employees = await httpClient.GetFromJsonAsync<List<Employee>>(
            "api/employees",
            cancellationToken);

        return employees ?? [];
    }
}
