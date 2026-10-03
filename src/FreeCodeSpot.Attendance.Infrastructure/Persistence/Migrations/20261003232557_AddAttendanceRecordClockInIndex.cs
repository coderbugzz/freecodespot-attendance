using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreeCodeSpot.Attendance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceRecordClockInIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_EmployeeId_ClockInUtc",
                table: "AttendanceRecords",
                columns: new[] { "EmployeeId", "ClockInUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecords_EmployeeId_ClockInUtc",
                table: "AttendanceRecords");
        }
    }
}
