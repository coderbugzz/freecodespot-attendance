using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FreeCodeSpot.Attendance.Web.Models;
using Microsoft.AspNetCore.Components.Authorization;

namespace FreeCodeSpot.Attendance.Web.Services;

/// <summary>
/// Calls the attendance API. Components depend on this instead of holding an
/// HttpClient and a route string of their own. Every call carries the API token
/// of the signed-in user.
/// </summary>
public sealed class EmployeeApiClient(
    HttpClient httpClient,
    AuthenticationStateProvider authenticationStateProvider,
    ILogger<EmployeeApiClient> logger)
{
    public async Task<IReadOnlyList<Employee>> GetEmployeesAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        await UseSignedInUserAsync();
        logger.LogInformation("Requesting the employee roster from {BaseAddress}", httpClient.BaseAddress);

        var route = includeInactive ? "api/employees?includeInactive=true" : "api/employees";
        var employees = await httpClient.GetFromJsonAsync<List<Employee>>(route, cancellationToken);

        return employees ?? [];
    }

    public async Task<Employee?> GetEmployeeAsync(int id, CancellationToken cancellationToken = default)
    {
        await UseSignedInUserAsync();
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
        await UseSignedInUserAsync();
        using var response = await httpClient.PostAsJsonAsync("api/employees", ToRequest(form), cancellationToken);
        return await ToSaveResultAsync(response, cancellationToken);
    }

    public async Task<SaveResult> UpdateAsync(int id, EmployeeFormModel form, CancellationToken cancellationToken = default)
    {
        await UseSignedInUserAsync();
        using var response = await httpClient.PutAsJsonAsync($"api/employees/{id}", ToRequest(form), cancellationToken);
        return await ToSaveResultAsync(response, cancellationToken);
    }

    public Task<SaveResult> DeactivateAsync(int id, CancellationToken cancellationToken = default) =>
        PostActionAsync($"api/employees/{id}/deactivate", cancellationToken);

    public Task<SaveResult> ActivateAsync(int id, CancellationToken cancellationToken = default) =>
        PostActionAsync($"api/employees/{id}/activate", cancellationToken);

    /// <summary>Checks the rows of a sheet and saves nothing.</summary>
    public Task<ImportResponse> PreviewImportAsync(byte[] file, CancellationToken cancellationToken = default) =>
        UploadAsync("api/employees/import/preview", file, cancellationToken);

    /// <summary>Saves the rows of a sheet that are fine, and skips the others.</summary>
    public Task<ImportResponse> ImportAsync(byte[] file, CancellationToken cancellationToken = default) =>
        UploadAsync("api/employees/import", file, cancellationToken);

    private async Task<ImportResponse> UploadAsync(string route, byte[] file, CancellationToken cancellationToken)
    {
        await UseSignedInUserAsync();

        using var content = new MultipartFormDataContent();
        var part = new ByteArrayContent(file);
        part.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        // The API does not need the name of the file the user chose. A name with a quote or a
        // line break in it would break the header, so the upload always has the same name.
        content.Add(part, "file", "employees.xlsx");

        using var response = await httpClient.PostAsync(route, content, cancellationToken);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            // The file cannot be used at all. The API says why, and the page shows the message.
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(cancellationToken);
            return ImportResponse.Refused(string.Join(" ", problem?.Errors.Values.SelectMany(errors => errors) ?? []));
        }

        response.EnsureSuccessStatusCode();
        var report = await response.Content.ReadFromJsonAsync<EmployeeImportReport>(cancellationToken)
            ?? throw new InvalidOperationException("The API returned an empty import report.");
        return ImportResponse.Of(report);
    }

    private async Task<SaveResult> PostActionAsync(string route, CancellationToken cancellationToken)
    {
        await UseSignedInUserAsync();
        using var response = await httpClient.PostAsync(route, content: null, cancellationToken);
        return await ToSaveResultAsync(response, cancellationToken);
    }

    private Task UseSignedInUserAsync() => httpClient.UseSignedInUserAsync(authenticationStateProvider);

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
