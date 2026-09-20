# FreeCodeSpot Attendance

Source code for the FreeCodeSpot article series that builds an **Employee Attendance System** with .NET 10, ASP.NET Core and Blazor.

Each published article corresponds to an immutable Git tag `article-NNN`. Check out the tag to see the code exactly as the article describes it.

## What is here

An ASP.NET Core minimal API that serves an employee roster, and a Blazor Web App that displays it.

```
src/FreeCodeSpot.Attendance.Domain           Employee record
src/FreeCodeSpot.Attendance.Application      EmployeeService and IEmployeeRepository
src/FreeCodeSpot.Attendance.Infrastructure   InMemoryEmployeeRepository, AddInfrastructure()
src/FreeCodeSpot.Attendance.Api              minimal API, GET /api/employees and GET /api/employees/{id}
src/FreeCodeSpot.Attendance.Web              Blazor Web App (interactive server), Employees page
tests/FreeCodeSpot.Attendance.UnitTests      xUnit tests for the service and repository
tests/FreeCodeSpot.Attendance.IntegrationTests   WebApplicationFactory tests against the API
```

The roster is held in memory and seeded with five employees at startup.

## Requirements

- .NET 10 SDK
- A trusted HTTPS development certificate (`dotnet dev-certs https --trust`)

## Running it

Build and test from the repository root:

```bash
dotnet build
dotnet test
```

Run the API and the Web app in two terminals:

```bash
cd src/FreeCodeSpot.Attendance.Api
dotnet run --launch-profile https
```

```bash
cd src/FreeCodeSpot.Attendance.Web
dotnet run --launch-profile https
```

| Project | URL |
|---|---|
| API | https://localhost:7020 |
| Web | https://localhost:7098 |

Open https://localhost:7098/employees to see the roster. The Web app reads the API address from `AttendanceApi:BaseUrl` in `appsettings.json`.

## Endpoints

| Method | Route | Returns |
|---|---|---|
| GET | `/api/employees` | the roster ordered by name |
| GET | `/api/employees/{id}` | one employee, or 404 |

## Articles

| Tag | Article |
|---|---|
| `article-001` | Project setup: solution structure, employee API, and the Blazor Employees page |
