using FreeCodeSpot.Attendance.Domain.Employees;
using Microsoft.EntityFrameworkCore;

namespace FreeCodeSpot.Attendance.Infrastructure.Persistence;

/// <summary>
/// The EF Core session for the attendance database. Table mappings live in
/// their own configuration classes so this file stays short as tables are added.
/// </summary>
public sealed class AttendanceDbContext(DbContextOptions<AttendanceDbContext> options)
    : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AttendanceDbContext).Assembly);
    }
}
