namespace FreeCodeSpot.Attendance.IntegrationTests;

/// <summary>
/// Every test class that talks to the database joins this collection, so they
/// share one AttendanceApiFactory and one test database, and never run in
/// parallel against it.
/// </summary>
[CollectionDefinition(Name)]
public sealed class AttendanceApiCollection : ICollectionFixture<AttendanceApiFactory>
{
    public const string Name = "Attendance API";
}
