namespace FreeCodeSpot.Attendance.Application.Reports;

/// <summary>
/// The rules the reports use, read from the Attendance section of appsettings.json.
/// An employee is late when the first clock-in of a working day comes after
/// WorkStart plus the grace minutes. Times of day are in the time zone of the API.
/// </summary>
public sealed class ReportOptions
{
    public TimeOnly WorkStart { get; set; } = new(8, 0);

    public int GraceMinutes { get; set; } = 10;

    /// <summary>The days of the week people are expected to work. There is no holiday calendar.</summary>
    public DayOfWeek[] WorkingDays { get; set; } = [];

    /// <summary>The working days to use when the setting is missing: Monday to Friday.</summary>
    public static readonly DayOfWeek[] DefaultWorkingDays =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];
}
