using FreeCodeSpot.Attendance.Application.Employees;
using FreeCodeSpot.Attendance.Domain.Employees;
using FreeCodeSpot.Attendance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreeCodeSpot.Attendance.Infrastructure.Employees;

/// <summary>
/// Reads the roster from SQL Server through EF Core. The queries are read-only,
/// so change tracking is switched off.
/// </summary>
public sealed class EfEmployeeRepository(AttendanceDbContext db) : IEmployeeRepository
{
    public IReadOnlyList<Employee> GetAll() =>
        db.Employees
            .AsNoTracking()
            .ToList();

    public Employee? GetById(int id) =>
        db.Employees
            .AsNoTracking()
            .FirstOrDefault(employee => employee.Id == id);
}
