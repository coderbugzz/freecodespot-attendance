using FreeCodeSpot.Attendance.Api.Auth;
using FreeCodeSpot.Attendance.Application.Reports;

namespace FreeCodeSpot.Attendance.Api.Endpoints;

/// <summary>
/// Maps the report route. Only managers can use it.
/// </summary>
public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports")
            .WithTags("Reports")
            .RequireAuthorization(Policies.ManagerOnly);

        // Without from and to, the current month up to today.
        group.MapGet("/", async (DateOnly? from, DateOnly? to, ReportService reports, CancellationToken cancellationToken) =>
                ToResult(await reports.GetReportAsync(from, to, cancellationToken)))
            .WithName("GetAttendanceReport")
            .Produces<AttendanceReport>()
            .ProducesValidationProblem();

        return app;
    }

    internal static IResult ToResult(ReportResult result) =>
        result.Outcome switch
        {
            ReportOutcome.Success => Results.Ok(result.Report),
            _ => Results.ValidationProblem(result.Errors.ToDictionary())
        };
}
