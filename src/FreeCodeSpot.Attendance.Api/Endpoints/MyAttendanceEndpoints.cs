using System.Globalization;
using System.Security.Claims;
using FreeCodeSpot.Attendance.Api.Auth;
using FreeCodeSpot.Attendance.Application.Attendance;
using FreeCodeSpot.Attendance.Application.Security;
using FreeCodeSpot.Attendance.Domain.Attendance;

namespace FreeCodeSpot.Attendance.Api.Endpoints;

/// <summary>
/// Maps the routes a signed-in user uses for their own attendance. The employee
/// comes from the employee_id claim in the token and never from the request, so
/// there is nothing in the URL that could point at somebody else.
/// </summary>
public static class MyAttendanceEndpoints
{
    public static IEndpointRouteBuilder MapMyAttendanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/attendance/me")
            .WithTags("My attendance")
            .RequireAuthorization(Policies.HasEmployeeRecord);

        group.MapGet("/", async (ClaimsPrincipal user, AttendanceService attendance, CancellationToken cancellationToken) =>
            {
                var mine = await attendance.GetMyAttendanceAsync(GetEmployeeId(user), cancellationToken: cancellationToken);
                return mine is null ? Results.NotFound() : Results.Ok(mine);
            })
            .WithName("GetMyAttendance")
            .Produces<MyAttendance>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/clock-in", async (ClaimsPrincipal user, AttendanceService attendance, CancellationToken cancellationToken) =>
                AttendanceEndpoints.ToResult(await attendance.ClockInAsync(GetEmployeeId(user), cancellationToken)))
            .WithName("ClockInMyself")
            .Produces<AttendanceRecord>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/clock-out", async (ClaimsPrincipal user, AttendanceService attendance, CancellationToken cancellationToken) =>
                AttendanceEndpoints.ToResult(await attendance.ClockOutAsync(GetEmployeeId(user), cancellationToken)))
            .WithName("ClockOutMyself")
            .Produces<AttendanceRecord>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    // The policy has already checked that the claim exists. A value that is not a number
    // would mean the token was built wrongly, so it is treated as an employee that does not exist.
    private static int GetEmployeeId(ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(AttendanceClaims.EmployeeId), CultureInfo.InvariantCulture, out var id) ? id : 0;
}
