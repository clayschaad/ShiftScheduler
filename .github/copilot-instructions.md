# ShiftScheduler
ShiftScheduler is a Blazor WebAssembly application for managing work shift schedules with calendar export capabilities. The application allows users to select shifts for each day of the month and export schedules as ICS (calendar) or PDF files.

Always reference these instructions first and fallback to search or bash commands only when you encounter unexpected information that does not match the info here.

## Working Effectively
- **CRITICAL**: Install .NET 9.0 SDK first:
  - `wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb`
  - `sudo dpkg -i packages-microsoft-prod.deb`
  - `sudo apt-get update`
  - `sudo apt-get remove -y dotnet-host-8.0 dotnet-runtime-8.0 aspnetcore-runtime-8.0 dotnet-sdk-8.0` (removes conflicts)
  - `sudo apt-get install -y dotnet-sdk-9.0`
  - Verify with `dotnet --version` (should show 9.0.x)

- Bootstrap and build the application:
  - `dotnet restore` -- takes 30 seconds. NEVER CANCEL. Set timeout to 60+ minutes.
  - `dotnet build` -- takes 15-20 seconds. NEVER CANCEL. Set timeout to 60+ minutes.
  - `dotnet clean` -- takes 3 seconds when needed

- Run the application:
  - Server: `cd Server && dotnet run` -- takes 15 seconds to start. NEVER CANCEL.
  - Access at: `http://localhost:5000`
  - Application will be available after Blazor WebAssembly loads (~10 seconds additional)

- No unit tests exist in this project:
  - `dotnet test` runs successfully but reports "Build succeeded" with 0 tests

## Validation
- **MANUAL VALIDATION REQUIREMENT**: Always test the full user workflow after making changes:
  1. Navigate to `http://localhost:5000`
  2. Wait for Blazor WebAssembly to load (look for calendar grid)
  3. Click shift buttons (⚫ for "Frei", 🌴 for "Urlaub") to select shifts
  4. Verify tooltips appear showing shift names
  5. Test export functionality by clicking "Export to ICS" or "Export to PDF"
  6. Verify files download correctly

- **API VALIDATION**: Test core endpoints:
  - `curl http://localhost:5000/api/shift/shifts` (should return shift configurations)
  - PDF export: `curl -X POST http://localhost:5000/api/shift/export_pdf -H "Content-Type: application/json" -d '{"2025-09-01": "Frei"}' --output test.pdf`

- The application has 2 known build warnings that are non-breaking:
  - Obsolete QuestPDF Image method warning
  - Possible null reference in IcsExportService

- No linting tools are configured. No specific formatting or linting commands to run.

## Validation Scenarios
After making changes, always run through these scenarios:
1. **Calendar Interaction**: Select different shifts for various days and verify visual feedback
2. **Export Functions**: Test both ICS and PDF export with selected shifts
3. **API Responses**: Verify shift data loads correctly via API calls
4. **Application Startup**: Confirm the app starts without errors and loads the UI

## Common Tasks

### Key Configuration Files
- `Server/appsettings.json`: Contains shift definitions with Name, Icon, MorningTime, AfternoonTime
- `Server/Properties/launchSettings.json`: Development server configuration (port 5000)
- All `.csproj` files target .NET 9.0

### Timing Expectations
- **NEVER CANCEL**: All build operations may take time but must complete
- `dotnet restore`: 30 seconds (first time), 1-2 seconds (subsequent)
- `dotnet build`: 15-20 seconds clean build, 5-10 seconds incremental
- `dotnet run` startup: 15 seconds until server ready + 10 seconds for WebAssembly load
- Application response: Immediate once loaded

### Troubleshooting
- If build fails with "does not support targeting .NET 9.0": Install .NET 9.0 SDK as described above
- If application won't start: Check that no other service is using port 5000
- If WebAssembly doesn't load: Wait longer, check browser console for JavaScript errors
- Export issues: Verify shift configurations in appsettings.json have proper time formats for ICS export

## C# Coding Standards
- Should use params collections (e.g., ```csharp
void Foo(params IReadOnlyList<string> values) => // actual implementation here.```)
- Using Lock instead of new object() makes the intent of the code clear and there also might be performance benefits due to special casing of the new type in the .NET runtime.
- Should use Primary constructors
- Should use collection expressions & spread operator
- Don't use ref readonly parameters

### Code Quality
- Write unit tests for new features and bug fixes whenever possible and it provides value
- Treat warnings as errors in builds to prevent minor issues from accumulating (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`)

### Namespace Guidelines
- Use consistent namespace structure following folder hierarchy
- Avoid deep nesting - keep namespaces reasonable length
- Group related functionality in same namespace

### API Controllers
- Use proper route attributes with versioning (e.g., `api/v1/`, `api/v2/`)
- Follow kebab-case for URLs where required, suppress warnings where legacy URLs exist
- Always include proper HTTP status code responses with SwaggerResponse attributes
- Validate input parameters and return appropriate error responses

### REST API Design
- Use HTTP verbs correctly (GET for queries, POST for commands, PUT for updates, DELETE for removal)
- Return appropriate HTTP status codes (200, 201, 400, 404, 409, 500)
- Use consistent error response format
- Support pagination for collection endpoints
- Include resource identifiers in URLs: `/api/v1/shifts/{shiftId}`
- Use plural nouns for resource collections: `/api/v1/shifts`

### Command API Design
- Commands are named imperative: `CreateShift`, `UpdateShift`
- Commands have no Pre-/Suffix "Command"

### Async Guidelines
- Always use `async`/`await` for I/O operations
- Include `CancellationToken` as last parameter in async methods
- Don't block on async code with `.Result` or `.Wait()` - if needed, use `SynchronousWaiter.WaitFor()`
- Name async methods with `Async` suffix

### Collections Guidelines
- Do not use immutable collections and try to remove them from the codebase.
- Use `IReadOnlySet<T>` and `IReadOnlyDictionary<T>` backed by mutable `HashSet<T>` and `Dictionary<T>` for very short lived collections (like most business logic)
- Prefer `List<T>` over `IList<T>` for mutable collections when concrete type is needed
- Use `HashSet<T>` for uniqueness, `List<T>` for ordering
- Use `FrozenSet<T>, FrozenDictionary<T>, ImmutableArray<T>` for long living data which is read often (e.g. Caches)
- Initialize collections at declaration when possible
- Avoid returning null collections - return empty collections instead

### Nullable Handling
- Use nullable reference types and try to remove #nullable disable from the codebase
- Use proper null checks and null-coalescing operators
- Use null-conditional operators where appropriate: `obj?.Property`

### Dependency Injection
- Use constructor dependency injection
- Register services with appropriate lifetime (Singleton, Scoped, Transient)
- Avoid service locator pattern

### API Documentation
- Document public APIs
- Include proper Swagger annotations
- Provide examples in documentation whenever possible
- Keep documentation up to date with code changes

### Error Handling
- Include meaningful error messages
- Log exceptions with appropriate severity levels
- Don't expose internal implementation details in error messages

### File Organization
- Use file-scoped namespaces
- Keep one class per file

### Performance
- Avoid unnecessary allocations
- Avoid calling methods that access APIs or databases inside loops

## Domain-Specific Patterns

### ID Types and Records
- Replace complex Id types with primitive types wherever possible
- Use readonly record structs for immutable data

### Service Interfaces
- Add interfaces to services only when absolutely necessary. Try to remove interfaces with a single implementation.
- Prefix service interfaces with `I`: `IMyService`
- Use specific return types rather than generic objects

### Equality Implementation
- Override `Equals` and `GetHashCode` consistently
- Implement `IEquatable<T>` for value types and data objects - but not for records
- Use proper null checks in equality methods
- Consider `ReferenceEquals` optimization for reference types

### Architecture
- Proper separation of concerns
- Following established patterns in the codebase
- Do not use any sort of "aggregate root" concept, instead use simple entities and value objects.

### Testing
- Ensure testability through proper dependency injection
- Mock external dependencies
- Test both success and error scenarios

### Common Instructions
- Don't write code comments
- Use var instead of explicit type, ex. var x = 4;
