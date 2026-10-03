using FreeCodeSpot.Attendance.Application.Timesheets;
using FreeCodeSpot.Attendance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreeCodeSpot.Attendance.Infrastructure.Timesheets;

/// <summary>
/// Reads timesheet records from SQL Server. The filter on the dates and the
/// employee runs in the database, and only the columns of TimesheetRecord are
/// returned, joined with the employee name. The ordering comes before the select,
/// because EF Core cannot translate an ordering on the members of the projected record.
/// </summary>
public sealed class EfTimesheetRepository(AttendanceDbContext db) : ITimesheetRepository
{
    public async Task<IReadOnlyList<TimesheetRecord>> GetRecordsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int? employeeId,
        CancellationToken cancellationToken = default) =>
        await (
            from record in db.AttendanceRecords.AsNoTracking()
            join employee in db.Employees on record.EmployeeId equals employee.Id
            where record.ClockInUtc >= fromUtc && record.ClockInUtc < toUtc
            where employeeId == null || record.EmployeeId == employeeId
            orderby employee.FullName, record.ClockInUtc
            select new TimesheetRecord(
                employee.Id,
                employee.FullName,
                employee.Department,
                record.Id,
                record.ClockInUtc,
                record.ClockOutUtc))
            .ToListAsync(cancellationToken);
}
