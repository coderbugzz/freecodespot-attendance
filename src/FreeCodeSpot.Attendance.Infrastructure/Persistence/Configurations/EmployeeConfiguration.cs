using FreeCodeSpot.Attendance.Domain.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreeCodeSpot.Attendance.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps Employee to the Employees table and seeds the starting roster.
/// The seed rows are part of the migration, so every new database starts
/// with the same five employees.
/// </summary>
public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");

        builder.HasKey(employee => employee.Id);

        builder.Property(employee => employee.FullName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(employee => employee.Department)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(employee => employee.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.HasIndex(employee => employee.Email)
            .IsUnique();

        builder.HasData(
            new Employee(1, "Regie Baquero", "Engineering", "regie@freecodespot.local"),
            new Employee(2, "Anna Cruz", "Engineering", "anna@freecodespot.local"),
            new Employee(3, "Marco Diaz", "Support", "marco@freecodespot.local"),
            new Employee(4, "Lina Reyes", "Human Resources", "lina@freecodespot.local"),
            new Employee(5, "Paolo Santos", "Operations", "paolo@freecodespot.local"));
    }
}
