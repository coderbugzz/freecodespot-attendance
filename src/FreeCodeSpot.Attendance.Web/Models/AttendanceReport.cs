namespace FreeCodeSpot.Attendance.Web.Models;

/// <summary>
/// The attendance report as the API returns it. The days and the times of day are in
/// the time zone named by TimeZoneId, so the page shows the clock-in times in that zone too.
/// </summary>
public record AttendanceReport(
    DateOnly From,
    DateOnly To,
    string TimeZoneId,
    TimeOnly WorkStart,
    int GraceMinutes,
    IReadOnlyList<EmployeeSummary> Summary,
    IReadOnlyList<LateArrival> LateArrivals,
    IReadOnlyList<EmployeeAbsences> Absences);

public record EmployeeSummary(
    int EmployeeId,
    string FullName,
    string Department,
    int DaysWorked,
    TimeSpan TotalWorked,
    int LateCount,
    int AbsentCount);

/// <summary>Late is counted from the start of work, not from the end of the grace period.</summary>
public record LateArrival(
    int EmployeeId,
    string FullName,
    string Department,
    DateOnly Date,
    DateTimeOffset ClockInUtc,
    TimeSpan Late);

public record EmployeeAbsences(int EmployeeId, string FullName, string Department, IReadOnlyList<DateOnly> Dates);
