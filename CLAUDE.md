# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Restore, build, run
dotnet restore
dotnet build
cd Server && dotnet run        # app at http://localhost:5000

# Run all tests
dotnet test

# Run a single test project
dotnet test ShiftScheduler.Services.Tests/

# Run a specific test
dotnet test --filter "FullyQualifiedName~CalendarEventBuilderTests"
```

Build warnings to ignore (non-breaking, pre-existing):
- Obsolete QuestPDF `Image` method
- Possible null reference in `IcsExportService`

## Git Workflow

- All features and fixes are developed on a dedicated branch (never commit directly to `main`)
- When merging to `main`, squash all branch commits into a single commit with a descriptive message

## Architecture

Blazor WebAssembly hosted on ASP.NET Core. The Server project serves both the API and the WASM client.

```
Client/      — Blazor WebAssembly frontend (Razor components, pages)
Server/      — ASP.NET Core host: API controllers, auth, DI wiring (Program.cs)
Services/    — All business logic: export, transport, calendar sync
Shared/      — DTOs shared between Client and Server
ShiftScheduler.Services.Tests/   — xUnit tests for Services layer
ShiftScheduler.Client.Tests/     — bUnit UI tests for Client components
Directory.Build.props            — Shared MSBuild properties (TargetFramework, Nullable, ImplicitUsings)
```

### Key data flow

1. The user selects shifts in the Blazor UI (`Client/Pages/Index.razor`)
2. Shift data (a `Dictionary<DateOnly, string>`) is POSTed to `ShiftController` in the Server
3. The controller delegates to services in `Services/` for ICS export, PDF export, transport lookup, or calendar sync
4. Configuration (shift definitions, transport settings, auth) lives in `Server/appsettings.json`

### Calendar sync providers

`ICalendarService` is the shared interface. Two implementations exist:
- `GoogleCalendarService` — OAuth token from cookie, Google Calendar API
- `NextcloudCalendarService` — CalDAV via `Ical.Net`, credentials from `appsettings.json`

`CalendarEventBuilder` contains shared logic for building calendar events and transport descriptions used by both providers.

The API endpoint `POST /api/shift/sync_calendar?provider=google|nextcloud` dispatches to the correct service; unknown providers return 400.

### Authentication

Cookie-based Google OAuth. The `AllowedEmails` authorization policy gates every `ShiftController` endpoint. Authorized emails are configured in `appsettings.json` under `Authentication:AuthorizedEmails`.

### Transport integration

`TransportService` → `TransportApiService` (HTTP) → Swiss public transport API (`transport.opendata.ch`). Results are cached via `IMemoryCache`. `TransportConnectionCalculator` applies safety buffers and selects the optimal connection.

## C# Conventions

- `var` everywhere instead of explicit types
- Primary constructors
- File-scoped namespaces, one class per file
- No code comments
- `async`/`await` for all I/O; async methods suffixed with `Async`; `CancellationToken` as last parameter
- `DateOnly` for dates, `TimeOnly` for times, `DateTimeOffset` (Europe/Zurich) for date+time
- Interfaces on services only when there are multiple implementations or tests require mocking; no `I`-prefix interfaces with a single implementation
- Use `params IReadOnlyList<T>` (params collections), `Lock` instead of `new object()`, collection expressions and spread operator
- `IReadOnlySet<T>` / `IReadOnlyDictionary<T>` backed by mutable types for short-lived collections; `FrozenSet<T>` / `FrozenDictionary<T>` / `ImmutableArray<T>` for long-lived cached data
- No immutable collections otherwise; avoid returning null collections
