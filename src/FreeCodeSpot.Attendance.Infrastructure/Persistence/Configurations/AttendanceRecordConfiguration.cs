using FreeCodeSpot.Attendance.Domain.Attendance;
using FreeCodeSpot.Attendance.Domain.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreeCodeSpot.Attendance.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps AttendanceRecord to the AttendanceRecords table. The unique index only
/// covers rows without a clock-out time, so an employee can have many closed
/// records but never two open ones.
/// </summary>
public sealed class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.ToTable("AttendanceRecords");

        builder.HasKey(record => record.Id);

        builder.Property(record => record.ClockInUtc)
            .IsRequired();

        // Employees are never deleted, so a record can never lose its employee.
        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(record => record.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(record => record.EmployeeId)
            .IsUnique()
            .HasFilter("[ClockOutUtc] IS NULL")
            .HasDatabaseName("IX_AttendanceRecords_OneOpenRecordPerEmployee");
    }
}
