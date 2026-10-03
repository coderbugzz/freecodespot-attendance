using FreeCodeSpot.Attendance.Domain.Employees;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FreeCodeSpot.Attendance.Infrastructure.Persistence;

/// <summary>
/// The EF Core session for the attendance database. It extends IdentityDbContext,
/// so the users, the roles and the employees live in one database. Table
/// mappings live in their own configuration classes so this file stays short as
/// tables are added.
/// </summary>
public sealed class AttendanceDbContext(DbContextOptions<AttendanceDbContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Employee> Employees => Set<Employee>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AttendanceDbContext).Assembly);
    }
}
