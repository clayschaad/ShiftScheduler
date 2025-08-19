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
### Project Structure
```
ShiftScheduler.sln          # Main solution file
├── Client/                 # Blazor WebAssembly frontend
│   ├── Pages/Index.razor   # Main calendar UI
│   └── Program.cs          # Client startup
├── Server/                 # ASP.NET Core host
│   ├── Program.cs          # Server startup and dependency injection
│   ├── Controllers/ShiftController.cs  # API endpoints
│   └── appsettings.json    # Shift configurations
├── Shared/                 # Shared models
│   └── Shift.cs            # Shift data model
└── Services/               # Business logic
    ├── ShiftService.cs     # Shift data management
    ├── IcsExportService.cs # Calendar export (uses Ical.Net)
    └── PdfExportService.cs # PDF export (uses QuestPDF)
```

### Key Configuration Files
- `Server/appsettings.json`: Contains shift definitions with Name, Icon, MorningTime, AfternoonTime
- `Server/Properties/launchSettings.json`: Development server configuration (port 5000)
- All `.csproj` files target .NET 9.0

### Dependencies
- **Client**: Microsoft.AspNetCore.Components.WebAssembly
- **Server**: Microsoft.AspNetCore.Components.WebAssembly.Server
- **Services**: QuestPDF (PDF generation), Ical.Net (calendar export)

### Timing Expectations
- **NEVER CANCEL**: All build operations may take time but must complete
- `dotnet restore`: 30 seconds (first time), 1-2 seconds (subsequent)
- `dotnet build`: 15-20 seconds clean build, 5-10 seconds incremental
- `dotnet run` startup: 15 seconds until server ready + 10 seconds for WebAssembly load
- Application response: Immediate once loaded

### Common Development Workflows
1. **Making UI Changes**: Edit `Client/Pages/Index.razor` or related files, then `dotnet run` in Server directory
2. **Adding New Shifts**: Update `Server/appsettings.json` "Shifts" section
3. **API Changes**: Modify `Server/Controllers/ShiftController.cs`
4. **Business Logic**: Update files in `Services/` directory
5. **Data Models**: Modify `Shared/Shift.cs`

### Troubleshooting
- If build fails with "does not support targeting .NET 9.0": Install .NET 9.0 SDK as described above
- If application won't start: Check that no other service is using port 5000
- If WebAssembly doesn't load: Wait longer, check browser console for JavaScript errors
- Export issues: Verify shift configurations in appsettings.json have proper time formats for ICS export

  ## Common Instructions
  - Don't write code comments
  - Use var instead of explicit type, ex. var x = 4;
