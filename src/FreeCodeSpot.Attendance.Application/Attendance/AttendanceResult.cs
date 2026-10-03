using FreeCodeSpot.Attendance.Domain.Attendance;

namespace FreeCodeSpot.Attendance.Application.Attendance;

public enum AttendanceOutcome
{
    Success,
    NotFound,
    Conflict
}

/// <summary>
/// The result of a clock-in or clock-out. The API turns it into a status code.
/// Message says why a request was refused, in words a person can read.
/// </summary>
public sealed record AttendanceResult(AttendanceOutcome Outcome, AttendanceRecord? Record, string? Message)
{
    public static AttendanceResult Success(AttendanceRecord record) => new(AttendanceOutcome.Success, record, null);

    public static AttendanceResult NotFound() => new(AttendanceOutcome.NotFound, null, null);

    public static AttendanceResult Conflict(string message) => new(AttendanceOutcome.Conflict, null, message);
}
