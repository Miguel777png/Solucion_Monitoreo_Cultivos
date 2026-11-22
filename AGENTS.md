# Repository Guidelines

## Project Structure & Module Organization

The .NET 8 ASP.NET Core MVC application is under `Web_Solucion_Monitoreo_Invernadero/`.

- `Controllers/`: MVC actions and the `/api/telemetria` API.
- `Models/`: Entity Framework entities and request DTOs.
- `Database/`: `ApplicationDbContext` and relationship configuration.
- `Services/`: greenhouse state, auditing, and scheduled irrigation.
- `Views/`: feature-based Razor views and shared navigation.
- `wwwroot/`: CSS, JavaScript, Bootstrap, and assets.
- `sqlscript/TextFile.txt`: MySQL schema and seed data.

No automated test project exists. Add one beside the web project.

## Architecture Overview

ESP32 devices post readings to `TelemetriaController`, which validates sensors, updates `EstadoInvernaderoService`, stores history in MySQL, evaluates thresholds, and returns irrigation commands. JavaScript polls authenticated API endpoints for live values. `RiegoProgramadoWorker` evaluates schedules in the background.

## Build, Test, and Development Commands

Run commands from the repository root:

```powershell
dotnet restore .\Web_Solucion_Monitoreo_Invernadero\Web_Solucion_Monitoreo_Invernadero.csproj
dotnet build .\Web_Solucion_Monitoreo_Invernadero\Web_Solucion_Monitoreo_Invernadero.csproj
dotnet run --project .\Web_Solucion_Monitoreo_Invernadero
dotnet test
```

`restore` downloads packages, `build` compiles C# and Razor, and `run` starts the application. `dotnet test` executes all test projects.

## Coding Style & Naming Conventions

Use four spaces in C# and Razor, and two in CSS and JavaScript. Keep nullable reference types enabled. Use PascalCase for classes, actions, properties, and public methods; camelCase for parameters and locals; and `_` prefixes for private fields. Async methods end in `Async`. Keep reusable behavior in `Services/`.

## Testing Guidelines

Prefer xUnit in `Web_Solucion_Monitoreo_Invernadero.Tests`. Use names such as `Login_WhenCredentialsAreInvalid_ReturnsError`. Cover authentication, telemetry validation, threshold rules, history, and irrigation scheduling. For UI changes, include desktop and mobile screenshots and verify the Dev Tunnel flow.

## Commit & Pull Request Guidelines

Existing history uses short messages such as `fix v5` and `PROTOTIPO V3.0`. New commits should be more descriptive and imperative, for example `Protect dashboard routes with cookie authentication`. Keep each commit focused. Pull requests should explain the behavior changed, database impact, manual test steps, and related issue; include screenshots for Razor/CSS changes.

## Security & Configuration Tips

Do not commit real credentials or tunnel URLs. Keep connection strings in user secrets or environment variables. Treat `sqlscript/TextFile.txt` cautiously: it begins with `DROP DATABASE` and must only be run against a disposable local database. Preserve the anonymous ESP32 ingestion endpoint intentionally; all user-facing pages and dashboard API reads must remain authenticated.
