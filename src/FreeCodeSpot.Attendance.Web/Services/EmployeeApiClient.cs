using System.Net;
using System.Net.Http.Json;
using FreeCodeSpot.Attendance.Web.Models;

namespace FreeCodeSpot.Attendance.Web.Services;

/// <summary>
/// Calls the attendance API. Components depend on this instead of holding an
/// HttpClient and a route string of their own.
/// </summary>
public sealed class EmployeeApiClient(HttpClient httpClient, ILogger<EmployeeApiClient> logger)
{
    public async Task<IReadOnlyList<Employee>> GetEmployeesAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Requesting the employee roster from {BaseAddress}", httpClient.BaseAddress);

        var route = includeInactive ? "api/employees?includeInactive=true" : "api/employees";
        var employees = await httpClient.GetFromJsonAsync<List<Employee>>(route, cancellationToken);

        return employees ?? [];
    }

    public async Task<Employee?> GetEmployeeAsync(int id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"api/employees/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Employee>(cancellationToken);
    }

    public async Task<SaveResult> CreateAsync(EmployeeFormModel form, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("api/employees", ToRequest(form), cancellationToken);
        return await ToSaveResultAsync(response, cancellationToken);
    }

    public async Task<SaveResult> UpdateAsync(int id, EmployeeFormModel form, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"api/employees/{id}", ToRequest(form), cancellationToken);
        return await ToSaveResultAsync(response, cancellationToken);
    }

    public Task<SaveResult> DeactivateAsync(int id, CancellationToken cancellationToken = default) =>
        PostActionAsync($"api/employees/{id}/deactivate", cancellationToken);

    public Task<SaveResult> ActivateAsync(int id, CancellationToken cancellationToken = default) =>
        PostActionAsync($"api/employees/{id}/activate", cancellationToken);

    private async Task<SaveResult> PostActionAsync(string route, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsync(route, content: null, cancellationToken);
        return await ToSaveResultAsync(response, cancellationToken);
    }

    private static object ToRequest(EmployeeFormModel form) =>
        new { form.FullName, form.Department, form.Email };

    private static async Task<SaveResult> ToSaveResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return SaveResult.Saved;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return SaveResult.Missing;
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(cancellationToken);
            return SaveResult.Invalid(problem?.Errors ?? []);
        }

        // Anything else is unexpected. Let the page show its error message.
        response.EnsureSuccessStatusCode();
        return SaveResult.Saved;
    }

    /// <summary>The part of the API's validation problem response the forms use.</summary>
    private sealed record ValidationProblemResponse(Dictionary<string, string[]> Errors);
}
