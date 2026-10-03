using FreeCodeSpot.Attendance.Application.Employees;
using FreeCodeSpot.Attendance.Domain.Attendance;

namespace FreeCodeSpot.Attendance.Application.Attendance;

/// <summary>
/// The clock-in and clock-out use cases. Every rule about when an employee may
/// clock in or out lives here so it can be unit tested without a database. The
/// clock comes from the TimeProvider, so tests can decide what time it is.
/// </summary>
public sealed class AttendanceService(
    IAttendanceRepository attendance,
    IEmployeeRepository employees,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Returns the active employees ordered by name, each with their clock-in
    /// time when they are clocked in. An employee who was deactivated while
    /// clocked in stays on the list until they clock out.
    /// </summary>
    public async Task<IReadOnlyList<EmployeeAttendanceStatus>> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        var roster = await employees.GetAllAsync(includeInactive: true, cancellationToken);
        var open = await attendance.GetOpenRecordsAsync(cancellationToken);
        var clockedInSince = open.ToDictionary(record => record.EmployeeId, record => record.ClockInUtc);

        return roster
            .Where(employee => employee.IsActive || clockedInSince.ContainsKey(employee.Id))
            .OrderBy(employee => employee.FullName, StringComparer.OrdinalIgnoreCase)
            .Select(employee => new EmployeeAttendanceStatus(
                employee.Id,
                employee.FullName,
                employee.Department,
                clockedInSince.TryGetValue(employee.Id, out var since) ? since : null))
            .ToList();
    }

    public async Task<AttendanceResult> ClockInAsync(int employeeId, CancellationToken cancellationToken = default)
    {
        var employee = await employees.GetByIdAsync(employeeId, cancellationToken);
        if (employee is null)
        {
            return AttendanceResult.NotFound();
        }

        if (!employee.IsActive)
        {
            return AttendanceResult.Conflict($"{employee.FullName} is inactive and cannot clock in.");
        }

        var alreadyClockedIn = AttendanceResult.Conflict($"{employee.FullName} is already clocked in.");

        if (await attendance.GetOpenRecordAsync(employeeId, cancellationToken) is not null)
        {
            return alreadyClockedIn;
        }

        var added = await attendance.TryAddAsync(
            new AttendanceRecord(0, employeeId, timeProvider.GetUtcNow()),
            cancellationToken);

        // Null means another request clocked this employee in between our check and our insert.
        return added is null ? alreadyClockedIn : AttendanceResult.Success(added);
    }

    public async Task<AttendanceResult> ClockOutAsync(int employeeId, CancellationToken cancellationToken = default)
    {
        var employee = await employees.GetByIdAsync(employeeId, cancellationToken);
        if (employee is null)
        {
            return AttendanceResult.NotFound();
        }

        // An inactive employee can still clock out, so nobody stays clocked in forever.
        var open = await attendance.GetOpenRecordAsync(employeeId, cancellationToken);
        if (open is null)
        {
            return AttendanceResult.Conflict($"{employee.FullName} is not clocked in.");
        }

        var closed = open with { ClockOutUtc = timeProvider.GetUtcNow() };
        await attendance.UpdateAsync(closed, cancellationToken);

        return AttendanceResult.Success(closed);
    }
}
