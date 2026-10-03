using FreeCodeSpot.Attendance.Application.Attendance;
using FreeCodeSpot.Attendance.Domain.Attendance;
using FreeCodeSpot.Attendance.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FreeCodeSpot.Attendance.Infrastructure.Attendance;

/// <summary>
/// Stores attendance records in SQL Server through EF Core. Like the employee
/// repository, nothing stays tracked between calls.
/// </summary>
public sealed class EfAttendanceRepository(AttendanceDbContext db) : IAttendanceRepository
{
    // SQL Server error numbers for a duplicate key in a unique index.
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public async Task<IReadOnlyList<AttendanceRecord>> GetOpenRecordsAsync(CancellationToken cancellationToken = default) =>
        await db.AttendanceRecords
            .AsNoTracking()
            .Where(record => record.ClockOutUtc == null)
            .ToListAsync(cancellationToken);

    public Task<AttendanceRecord?> GetOpenRecordAsync(int employeeId, CancellationToken cancellationToken = default) =>
        db.AttendanceRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(
                record => record.EmployeeId == employeeId && record.ClockOutUtc == null,
                cancellationToken);

    public async Task<IReadOnlyList<AttendanceRecord>> GetRecentRecordsAsync(int employeeId, int count, CancellationToken cancellationToken = default) =>
        await db.AttendanceRecords
            .AsNoTracking()
            .Where(record => record.EmployeeId == employeeId)
            .OrderByDescending(record => record.ClockInUtc)
            .Take(count)
            .ToListAsync(cancellationToken);

    public async Task<AttendanceRecord?> TryAddAsync(AttendanceRecord record, CancellationToken cancellationToken = default)
    {
        db.AttendanceRecords.Add(record);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return record;
        }
        catch (DbUpdateException exception) when (IsDuplicateOpenRecord(exception))
        {
            return null;
        }
        finally
        {
            db.Entry(record).State = EntityState.Detached;
        }
    }

    public async Task UpdateAsync(AttendanceRecord record, CancellationToken cancellationToken = default)
    {
        db.AttendanceRecords.Update(record);
        await db.SaveChangesAsync(cancellationToken);
        db.Entry(record).State = EntityState.Detached;
    }

    private static bool IsDuplicateOpenRecord(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation };
}
