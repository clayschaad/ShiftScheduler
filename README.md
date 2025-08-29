# ShiftScheduler

A modern Blazor WebAssembly application for managing work shift schedules with calendar export capabilities and transport integration.

![Login Screen](https://github.com/user-attachments/assets/23aed472-7dbb-430f-81d1-b2f763330384)

## Overview

ShiftScheduler is a web-based shift management system that allows users to:
- Plan and visualize work shifts using an intuitive calendar interface
- Export schedules to industry-standard formats (ICS and PDF)
- Calculate optimal transport connections for work commutes
- Manage multiple shift types with customizable icons and time periods
- Secure access through Google OAuth authentication

Perfect for individuals or small teams who need to organize shift work, track schedules, and integrate with existing calendar systems.

## Key Features

### 📅 **Calendar-Based Shift Management**
- Interactive monthly calendar view with easy shift selection
- Switch between current and next month
- Visual shift indicators with customizable icons and colors
- Responsive design for desktop and mobile devices

### 🔄 **Export Capabilities**
- **ICS Export**: Import schedules directly into Google Calendar, Outlook, or any calendar application
- **PDF Export**: Generate printable shift schedules with professional formatting
- One-click export functionality

### 🚆 **Transport Integration**
- Automatic calculation of travel times between configurable stations
- Integration with Swiss public transport API (transport.opendata.ch)
- Safety buffer calculations for reliable commute planning
- Display of optimal departure and arrival times

### 🔐 **Secure Authentication**
- Google OAuth integration for secure access
- Configurable authorized email addresses
- Session management with proper login/logout functionality

### ⚙️ **Flexible Configuration**
- **Shift Types**: Fully customizable shift definitions with:
  - Custom names and icons (emoji or PNG images)
  - Morning and afternoon time periods
  - Flexible time format support
- **Transport Settings**: Configurable start/end stations, API parameters, and timing buffers
- **Persistent Configuration**: Settings survive application restarts and container rebuilds

### 🐳 **Docker Ready**
- Container-friendly with persistent external configuration
- Easy deployment with Docker Compose
- Configuration backup and version control support

## Available Shift Types (Default Configuration)

| Icon | Name | Morning Time | Afternoon Time | Description |
|------|------|--------------|----------------|-------------|
| ⚫ | Frei | - | - | Free day / Day off |
| 🌴 | Urlaub | - | - | Vacation day |
| 🛑 | Pause | 10:00-10:15 | 15:00-15:15 | Break periods |
| 🌅 | Früh | 06:00-14:00 | - | Early shift |
| 🌆 | Spät | - | 14:00-22:00 | Late shift |
| ☀️ | Tag | 08:00-12:00 | 13:00-17:00 | Day shift |

*All shift types are fully configurable through the application interface.*

## Configuration Options

### Authentication Settings
```json
{
  "Authentication": {
    "Google": {
      "ClientId": "your-google-client-id.apps.googleusercontent.com",
      "ClientSecret": "your-google-client-secret"
    },
    "AuthorizedEmails": [
      "user1@gmail.com",
      "user2@example.com"
    ]
  }
}
```

### Transport Configuration
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

### Shift Definitions
```json
{
  "Shifts": [
    {
      "Name": "Custom Shift",
      "Icon": "🎯",
      "MorningTime": "09:00-13:00",
      "AfternoonTime": "14:00-18:00"
    }
  ]
}
```

## Installation & Setup

### Prerequisites
- .NET 9.0 SDK
- Modern web browser
- Google Cloud Console account (for authentication)

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

3. **Configure authentication** (see [Authentication Setup Guide](authentication-setup.md))
   - Create Google OAuth application
   - Update `Server/appsettings.json` with your credentials
   - Add authorized email addresses

4. **Build and run**
   ```bash
   dotnet build
   cd Server
   dotnet run
   ```

5. **Access the application**
   - Navigate to `http://localhost:5000`
   - Sign in with your Google account
   - Start planning your shifts!

### Docker Deployment

```yaml
version: '3.8'
services:
  shiftscheduler:
    build: .
    ports:
      - "5000:5000"
    volumes:
      - ./config:/app/config  # Persistent configuration
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
```

See [Docker Configuration Guide](DOCKER_CONFIG.md) for detailed deployment instructions.

## Usage

### Planning Shifts
1. Navigate between months using the month selection buttons
2. Click on any day to cycle through available shift types
3. Shifts are automatically saved and persist across sessions
4. Hover over shifts to see detailed time information

### Exporting Schedules
- **ICS Export**: Click "Export to ICS" to download a calendar file compatible with all major calendar applications
- **PDF Export**: Click "Export to PDF" to generate a printable schedule document

### Managing Configuration
1. Click the "⚙️ Configuration" button to access settings
2. **Shift Management**: Add, edit, or remove shift types
3. **Transport Settings**: Configure stations and timing preferences
4. **Import/Export**: Backup and restore configuration settings

### Transport Integration
When transport is configured, the application automatically:
- Calculates optimal departure times for work commutes
- Displays train connections and travel duration
- Applies safety buffers for reliable planning
- Shows both morning and afternoon journey options

## Technical Architecture

- **Frontend**: Blazor WebAssembly (.NET 9.0)
- **Backend**: ASP.NET Core Web API (.NET 9.0)
- **Authentication**: Google OAuth 2.0
- **Export**: QuestPDF for PDF generation, custom ICS implementation
- **Transport**: Swiss Public Transport API integration
- **Configuration**: JSON-based with external persistence support

## Documentation

- [📚 Authentication Setup Guide](authentication-setup.md) - Complete Google OAuth configuration
- [🐳 Docker Configuration Guide](DOCKER_CONFIG.md) - Containerization and deployment
- [📄 License](LICENSE) - Apache 2.0 License

## Contributing

This project follows standard .NET development practices:
- Uses .NET 9.0 target framework
- Implements nullable reference types
- Follows established architectural patterns
- Includes comprehensive configuration options

## Support

For issues, questions, or feature requests, please use the GitHub issue tracker.

## License

This project is licensed under the Apache License 2.0 - see the [LICENSE](LICENSE) file for details.