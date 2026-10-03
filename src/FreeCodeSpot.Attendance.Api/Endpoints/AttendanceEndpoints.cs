using FreeCodeSpot.Attendance.Api.Auth;
using FreeCodeSpot.Attendance.Application.Attendance;
using FreeCodeSpot.Attendance.Application.Timesheets;
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

        // Without from and to, the last seven days. Without employeeId, every employee.
        group.MapGet("/timesheet", async (DateOnly? from, DateOnly? to, int? employeeId, TimesheetService timesheets, CancellationToken cancellationToken) =>
                ToResult(await timesheets.GetTimesheetAsync(from, to, employeeId, cancellationToken)))
            .WithName("GetTimesheet")
            .Produces<Timesheet>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

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

    internal static IResult ToResult(TimesheetResult result) =>
        result.Outcome switch
        {
            TimesheetOutcome.Success => Results.Ok(result.Timesheet),
            TimesheetOutcome.NotFound => Results.NotFound(),
            _ => Results.ValidationProblem(result.Errors.ToDictionary())
        };

    internal static IResult ToResult(AttendanceResult result) =>
        result.Outcome switch
        {
            AttendanceOutcome.Success => Results.Ok(result.Record),
            AttendanceOutcome.NotFound => Results.NotFound(),
            _ => Results.Problem(result.Message, statusCode: StatusCodes.Status409Conflict)
        };
}
