using FreeCodeSpot.Attendance.Api.Auth;
using FreeCodeSpot.Attendance.Application.Attendance;
using FreeCodeSpot.Attendance.Domain.Attendance;

namespace FreeCodeSpot.Attendance.Api.Endpoints;

/// <summary>
/// Maps the attendance routes. Only managers can use them.
/// </summary>
public static class AttendanceEndpoints
{
    public static IEndpointRouteBuilder MapAttendanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/attendance")
            .WithTags("Attendance")
            .RequireAuthorization(Policies.ManagerOnly);

        group.MapGet("/", async (AttendanceService attendance, CancellationToken cancellationToken) =>
                Results.Ok(await attendance.GetStatusAsync(cancellationToken)))
            .WithName("GetAttendanceStatus")
            .Produces<IReadOnlyList<EmployeeAttendanceStatus>>();

        group.MapPost("/{employeeId:int}/clock-in", async (int employeeId, AttendanceService attendance, CancellationToken cancellationToken) =>
                ToResult(await attendance.ClockInAsync(employeeId, cancellationToken)))
            .WithName("ClockIn")
            .Produces<AttendanceRecord>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{employeeId:int}/clock-out", async (int employeeId, AttendanceService attendance, CancellationToken cancellationToken) =>
                ToResult(await attendance.ClockOutAsync(employeeId, cancellationToken)))
            .WithName("ClockOut")
            .Produces<AttendanceRecord>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static IResult ToResult(AttendanceResult result) =>
        result.Outcome switch
        {
            AttendanceOutcome.Success => Results.Ok(result.Record),
            AttendanceOutcome.NotFound => Results.NotFound(),
            _ => Results.Problem(result.Message, statusCode: StatusCodes.Status409Conflict)
        };
}
