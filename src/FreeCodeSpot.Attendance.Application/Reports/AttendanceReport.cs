namespace FreeCodeSpot.Attendance.Application.Reports;

/// <summary>
/// Late arrivals, absences and a summary per employee between two dates, both included.
/// The dates are days in the time zone named by TimeZoneId, and WorkStart and
/// GraceMinutes are the rules the report applied.
/// </summary>
public sealed record AttendanceReport(
    DateOnly From,
    DateOnly To,
    string TimeZoneId,
    TimeOnly WorkStart,
    int GraceMinutes,
    IReadOnlyList<EmployeeSummary> Summary,
    IReadOnlyList<LateArrival> LateArrivals,
    IReadOnlyList<EmployeeAbsences> Absences);

/// <summary>
/// What one employee did in the range. TotalWorked adds up the closed records only.
/// </summary>
public sealed record EmployeeSummary(
    int EmployeeId,
    string FullName,
    string Department,
    int DaysWorked,
    TimeSpan TotalWorked,
    int LateCount,
    int AbsentCount);

/// <summary>A working day when the first clock-in came after the grace period. Late is counted from WorkStart.</summary>
public sealed record LateArrival(
    int EmployeeId,
    string FullName,
    string Department,
    DateOnly Date,
    DateTimeOffset ClockInUtc,
    TimeSpan Late);

/// <summary>The working days an active employee has no record on, oldest first.</summary>
public sealed record EmployeeAbsences(
    int EmployeeId,
    string FullName,
    string Department,
    IReadOnlyList<DateOnly> Dates);
