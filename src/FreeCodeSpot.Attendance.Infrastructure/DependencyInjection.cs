using FreeCodeSpot.Attendance.Application.Employees;
using FreeCodeSpot.Attendance.Infrastructure.Employees;
using Microsoft.Extensions.DependencyInjection;

namespace FreeCodeSpot.Attendance.Infrastructure;

/// <summary>
/// Registers the Infrastructure implementations. Keeping this here means the
/// API never names a concrete storage class.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IEmployeeRepository, InMemoryEmployeeRepository>();
        return services;
    }
}
