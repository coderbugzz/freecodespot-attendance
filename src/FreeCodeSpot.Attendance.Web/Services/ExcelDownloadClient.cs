using System.Globalization;
using System.Net.Http.Headers;

namespace FreeCodeSpot.Attendance.Web.Services;

/// <summary>A file the API sent, ready to be passed on to the browser.</summary>
public sealed record DownloadedFile(byte[] Content, string ContentType, string FileName);

/// <summary>
/// Gets the .xlsx files of the API for the browser. A download is a plain link, not a call
/// from a component, so the Web app asks the API on behalf of the browser. It takes the
/// token of the signed-in user from the sign-in cookie.
/// </summary>
public sealed class ExcelDownloadClient(HttpClient httpClient)
{
    public Task<DownloadedFile?> GetReportAsync(DateOnly? from, DateOnly? to, string? token, CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        if (from is { } start) query.Add($"from={start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        if (to is { } end) query.Add($"to={end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");

        var route = query.Count == 0 ? "api/reports/export" : $"api/reports/export?{string.Join("&", query)}";
        return GetAsync(route, token, cancellationToken);
    }

    public Task<DownloadedFile?> GetEmployeeTemplateAsync(string? token, CancellationToken cancellationToken = default) =>
        GetAsync("api/employees/import/template", token, cancellationToken);

    /// <summary>Returns null when the API refuses the request, for example without a Manager token.</summary>
    private async Task<DownloadedFile?> GetAsync(string route, string? token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var disposition = response.Content.Headers.ContentDisposition;
        return new DownloadedFile(
            await response.Content.ReadAsByteArrayAsync(cancellationToken),
            response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream",
            disposition?.FileNameStar ?? disposition?.FileName?.Trim('"') ?? "download.xlsx");
    }
}
