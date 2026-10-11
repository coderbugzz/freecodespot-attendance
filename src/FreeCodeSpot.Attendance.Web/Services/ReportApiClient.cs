using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using FreeCodeSpot.Attendance.Web.Models;
using Microsoft.AspNetCore.Components.Authorization;

namespace FreeCodeSpot.Attendance.Web.Services;

/// <summary>
/// Calls the report route of the API with the token of the signed-in user.
/// </summary>
public sealed class ReportApiClient(HttpClient httpClient, AuthenticationStateProvider authenticationStateProvider)
{
    /// <summary>Without dates, the API answers with the current month up to today.</summary>
    public async Task<ReportResponse> GetReportAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        await httpClient.UseSignedInUserAsync(authenticationStateProvider);

        var query = new List<string>();
        if (from is { } start) query.Add($"from={start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        if (to is { } end) query.Add($"to={end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");

        using var response = await httpClient.GetAsync(
            query.Count == 0 ? "api/reports" : $"api/reports?{string.Join("&", query)}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            // The API names the problem for each field. The page only needs the messages.
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(cancellationToken);
            var messages = problem?.Errors.Values.SelectMany(errors => errors) ?? [];
            return ReportResponse.Refused(string.Join(" ", messages));
        }

        response.EnsureSuccessStatusCode();
        var report = await response.Content.ReadFromJsonAsync<AttendanceReport>(cancellationToken)
            ?? throw new InvalidOperationException("The API returned an empty report.");
        return ReportResponse.Of(report);
    }

    /// <summary>The part of the API's validation problem response the report page shows.</summary>
    private sealed record ValidationProblemResponse(Dictionary<string, string[]> Errors);
}
