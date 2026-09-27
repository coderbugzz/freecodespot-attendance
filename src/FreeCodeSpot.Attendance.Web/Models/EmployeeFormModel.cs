using System.ComponentModel.DataAnnotations;

namespace FreeCodeSpot.Attendance.Web.Models;

/// <summary>
/// What the add and edit forms bind to. The attributes give the user instant
/// feedback in the browser; the API checks the same rules again, and it is the
/// only place that can tell whether an email is already taken.
/// </summary>
public sealed class EmployeeFormModel
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(100, ErrorMessage = "Name must be 100 characters or fewer.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Department is required.")]
    [StringLength(50, ErrorMessage = "Department must be 50 characters or fewer.")]
    public string Department { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [StringLength(256, ErrorMessage = "Email must be 256 characters or fewer.")]
    [EmailAddress(ErrorMessage = "Email is not a valid email address.")]
    public string Email { get; set; } = string.Empty;

    public static EmployeeFormModel From(Employee employee) => new()
    {
        FullName = employee.FullName,
        Department = employee.Department,
        Email = employee.Email
    };
}
