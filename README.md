# ShiftScheduler

A Blazor WebAssembly application for managing work shift schedules with calendar export and transport integration.

## Overview

ShiftScheduler is a web-based shift management system that allows users to:
- Plan and visualize work shifts using a monthly calendar interface
- Export schedules to ICS and PDF formats
- Sync shifts directly to Google Calendar or Nextcloud Calendar
- View optimal transport connections for work commutes
- Secure access through Google OAuth authentication

## Key Features

### Calendar-Based Shift Management
- Monthly calendar view with day-by-day shift assignment
- Toggle between current and next month
- Mobile-friendly week layout alongside the desktop grid view

### Export & Sync
- **ICS Export**: Download a calendar file compatible with any calendar application
- **PDF Export**: Generate a printable monthly schedule
- **Google Calendar Sync**: Direct sync to a selected Google Calendar; existing app-managed events are updated, other events are untouched
- **Nextcloud Calendar Sync**: CalDAV-based sync to a selected Nextcloud calendar

### Transport Integration
- Calculates optimal departure and arrival times for work commutes
- Integrates with the Swiss public transport API (`transport.opendata.ch`)
- Displays morning and afternoon connections alongside each shift
- Configurable safety buffers and break durations

### Authentication
- Google OAuth 2.0 with a configurable list of authorized email addresses

### Configuration
- Shift definitions (name, emoji icon, morning/afternoon time ranges) managed via the in-app configuration dialog
- Transport settings (stations, API URL, timing buffers) configurable in-app
- Configuration can be exported and imported as JSON for backup

## Default Shift Types

| Icon | Name | Morning Time | Afternoon Time |
|------|------|--------------|----------------|
| ⚫ | Frei | — | — |
| 🌴 | Urlaub | — | — |
| 🌅 | Früh | 06:00–14:00 | — |
| 🌆 | Spät | — | 14:00–22:00 |
| ☀️ | Tag | 08:00–12:00 | 13:00–17:00 |
| (icon) | Pause | 10:00–10:15 | 15:00–15:15 |

All shift types are configurable through the in-app settings dialog.

## Configuration

### Authentication (`Server/appsettings.json`)
```json
{
  "Authentication": {
    "Google": {
      "ClientId": "your-google-client-id.apps.googleusercontent.com",
      "ClientSecret": "your-google-client-secret"
    },
    "AuthorizedEmails": [
      "user@gmail.com"
    ]
  }
}
```

### Nextcloud CalDAV
```json
{
  "Nextcloud": {
    "BaseUrl": "https://your-nextcloud-instance.example.com",
    "Username": "your-username",
    "AppPassword": "your-app-password"
  }
}
```

### Transport
```json
{
  "Transport": {
    "StartStation": "Zurich",
    "EndStation": "Basel",
    "ApiBaseUrl": "http://transport.opendata.ch/v1",
    "SafetyBufferMinutes": 30,
    "MinBreakMinutes": 60,
    "MaxEarlyArrivalMinutes": 60,
    "MaxLateArrivalMinutes": 15,
    "CacheDurationDays": 1
  }
}
```

## Installation & Setup

### Prerequisites
- .NET 9.0 SDK
- Google Cloud Console account (for OAuth)

### Quick Start

1. **Clone the repository**
   ```bash
   git clone https://github.com/clayschaad/ShiftScheduler.git
   cd ShiftScheduler
   ```

2. **Install dependencies**
   ```bash
   dotnet restore
   ```

3. **Configure authentication**
   - See [Authentication Setup Guide](authentication-setup.md)
   - Update `Server/appsettings.json` with your Google OAuth credentials and authorized emails

4. **Build and run**
   ```bash
   dotnet build
   cd Server
   dotnet run
   ```

5. **Open the app**
   - Navigate to `http://localhost:5000`
   - Sign in with your Google account

## Documentation

- [Authentication Setup Guide](authentication-setup.md) — Google OAuth configuration
- [Docker Configuration Guide](DOCKER_CONFIG.md) — Containerized deployment

## License

Apache License 2.0 — see the [LICENSE](LICENSE) file for details.
