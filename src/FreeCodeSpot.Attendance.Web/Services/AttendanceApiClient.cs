using System.Net;
using System.Globalization;
using System.Net.Http.Json;
using FreeCodeSpot.Attendance.Web.Models;
using Microsoft.AspNetCore.Components.Authorization;

namespace FreeCodeSpot.Attendance.Web.Services;

/// <summary>
/// Calls the attendance routes of the API with the token of the signed-in user.
/// </summary>
public sealed class AttendanceApiClient(HttpClient httpClient, AuthenticationStateProvider authenticationStateProvider)
{
    public async Task<IReadOnlyList<EmployeeAttendanceStatus>> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        await httpClient.UseSignedInUserAsync(authenticationStateProvider);

        var status = await httpClient.GetFromJsonAsync<List<EmployeeAttendanceStatus>>("api/attendance", cancellationToken);
        return status ?? [];
    }

    /// <summary>The signed-in user's own status and latest records. The API knows who they are from the token.</summary>
    public async Task<MyAttendance?> GetMineAsync(CancellationToken cancellationToken = default)
    {
        await httpClient.UseSignedInUserAsync(authenticationStateProvider);

        return await httpClient.GetFromJsonAsync<MyAttendance>("api/attendance/me", cancellationToken);
    }

    /// <summary>Without dates, the API answers with the last seven days.</summary>
    public Task<TimesheetResponse> GetTimesheetAsync(DateOnly? from, DateOnly? to, int? employeeId, CancellationToken cancellationToken = default) =>
        GetTimesheetAsync("api/attendance/timesheet", from, to, employeeId, cancellationToken);

    public Task<TimesheetResponse> GetMyTimesheetAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default) =>
        GetTimesheetAsync("api/attendance/me/timesheet", from, to, employeeId: null, cancellationToken);

    public Task<ClockResult> ClockInMyselfAsync(CancellationToken cancellationToken = default) =>
        PostAsync("api/attendance/me/clock-in", cancellationToken);

    public Task<ClockResult> ClockOutMyselfAsync(CancellationToken cancellationToken = default) =>
        PostAsync("api/attendance/me/clock-out", cancellationToken);

    public Task<ClockResult> ClockInAsync(int employeeId, CancellationToken cancellationToken = default) =>
        PostAsync($"api/attendance/{employeeId}/clock-in", cancellationToken);

    public Task<ClockResult> ClockOutAsync(int employeeId, CancellationToken cancellationToken = default) =>
        PostAsync($"api/attendance/{employeeId}/clock-out", cancellationToken);

    private async Task<TimesheetResponse> GetTimesheetAsync(
        string route,
        DateOnly? from,
        DateOnly? to,
        int? employeeId,
        CancellationToken cancellationToken)
    {
        await httpClient.UseSignedInUserAsync(authenticationStateProvider);

        var query = new List<string>();
        if (from is { } start) query.Add($"from={start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        if (to is { } end) query.Add($"to={end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        if (employeeId is { } id) query.Add($"employeeId={id}");

        using var response = await httpClient.GetAsync(
            query.Count == 0 ? route : $"{route}?{string.Join("&", query)}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            // The API names the problem for each field. The page only needs the messages.
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(cancellationToken);
            var messages = problem?.Errors.Values.SelectMany(errors => errors) ?? [];
            return TimesheetResponse.Refused(string.Join(" ", messages));
        }

        response.EnsureSuccessStatusCode();
        var timesheet = await response.Content.ReadFromJsonAsync<Timesheet>(cancellationToken)
            ?? throw new InvalidOperationException("The API returned an empty timesheet.");
        return TimesheetResponse.Of(timesheet);
    }

    private async Task<ClockResult> PostAsync(string route, CancellationToken cancellationToken)
    {
        await httpClient.UseSignedInUserAsync(authenticationStateProvider);

        using var response = await httpClient.PostAsync(route, content: null, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return ClockResult.Done;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return ClockResult.Refused("That employee could not be found.");
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            // The API explains the refusal in the detail of its problem response.
            var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(cancellationToken);
            return ClockResult.Refused(problem?.Detail ?? "The request was refused.");
        }

        // Anything else is unexpected. Let the page show its error message.
        response.EnsureSuccessStatusCode();
        return ClockResult.Done;
    }

    /// <summary>The part of the API's problem response the page shows.</summary>
    private sealed record ProblemResponse(string? Detail);

    /// <summary>The part of the API's validation problem response the timesheet pages show.</summary>
    private sealed record ValidationProblemResponse(Dictionary<string, string[]> Errors);
}
