using FreeCodeSpot.Attendance.Application.Employees;
using FreeCodeSpot.Attendance.Domain.Employees;
using FreeCodeSpot.Attendance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreeCodeSpot.Attendance.Infrastructure.Employees;

/// <summary>
/// Stores the roster in SQL Server through EF Core. Nothing stays tracked
/// between calls: reads use AsNoTracking, and writes detach the record once it
/// is saved. Employee is an immutable record, so an update always arrives as a
/// new instance, and a tracked old one would clash with it.
/// </summary>
public sealed class EfEmployeeRepository(AttendanceDbContext db) : IEmployeeRepository
{
    public async Task<IReadOnlyList<Employee>> GetAllAsync(bool includeInactive, CancellationToken cancellationToken = default)
    {
        var query = db.Employees.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(employee => employee.IsActive);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public Task<Employee?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        db.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(employee => employee.Id == id, cancellationToken);

    // SQL Server's default collation compares case-insensitively, so
    // "Anna@..." and "anna@..." count as the same email, like the unique index.
    public Task<bool> EmailExistsAsync(string email, int? exceptId, CancellationToken cancellationToken = default) =>
        db.Employees.AnyAsync(
            employee => employee.Email == email && (exceptId == null || employee.Id != exceptId),
            cancellationToken);

    public async Task<Employee> AddAsync(Employee employee, CancellationToken cancellationToken = default)
    {
        db.Employees.Add(employee);
        await db.SaveChangesAsync(cancellationToken);
        db.Entry(employee).State = EntityState.Detached;

        // EF Core wrote the new identity value into employee.Id.
        return employee;
    }

    public async Task UpdateAsync(Employee employee, CancellationToken cancellationToken = default)
    {
        db.Employees.Update(employee);
        await db.SaveChangesAsync(cancellationToken);
        db.Entry(employee).State = EntityState.Detached;
    }
}
