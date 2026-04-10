# Nextcloud Calendar Sync — Design Spec

**Date:** 2026-04-10  
**Status:** Approved

---

## Overview

Add a second calendar sync target (Nextcloud CalDAV) alongside the existing Google Calendar sync. Shared logic is extracted into a common interface and a static helper, keeping both service implementations independent and self-contained.

---

## Architecture

### Shared interface: `ICalendarService`

Replaces `IGoogleCalendarService`. Both providers implement this interface:

```csharp
public interface ICalendarService
{
    Task<List<CalendarInfo>> GetCalendarsAsync();
    Task SyncShiftsToCalendarAsync(string calendarId, List<ShiftWithTransport> shifts);
}

public class CalendarInfo
{
    public string Id { get; set; }
    public string Summary { get; set; }
}
```

### Shared helper: `CalendarEventBuilder` (static class)

Extracts pure data-transformation logic currently inside `GoogleCalendarService`:

- `FormatTransportInfo(TransportConnection)` → string
- `BuildEventContent(Shift, DateTimeOffset start, DateTimeOffset end, TransportConnection?)` → returns a shared `CalendarEventData` record (summary + description)

No orchestration logic lives here — just pure functions with no side effects.

### `GoogleCalendarService`

- Implements `ICalendarService`
- Refactored to use `CalendarEventBuilder` for event content
- All Google client library calls remain here
- `GetCalendarsAsync()` returns `List<CalendarInfo>` mapped from Google's `CalendarListEntry`
- Identification of owned events via `ExtendedProperties.Private["shiftSchedulerEvent"]` unchanged

### `NextcloudCalendarService`

- Implements `ICalendarService`
- Uses `HttpClient` with Basic Auth (username + app password from configuration)
- CalDAV operations:
  - **List calendars**: `PROPFIND` on `/remote.php/dav/calendars/<username>/`, depth 1, parse `displayname` and `resourcetype`
  - **Delete existing events in range**: `REPORT` (CalDAV `calendar-query`) to find VEVENTs with `X-SHIFT-SCHEDULER:true` property in the date range, then `DELETE` each by URL
  - **Create events**: `PUT` a VEVENT (iCalendar format using the existing `Ical.Net` dependency) to `<calendar-url>/<uid>.ics`. Each event includes `X-SHIFT-SCHEDULER:true` as a custom property for future identification.
- UIDs are generated deterministically: `shift-<shiftName>-<dateISO>-<morning|afternoon>` to allow idempotent re-sync if needed
- Retry logic mirrors Google's exponential backoff, extracted from `GoogleCalendarService` into a shared `RetryHelper` static method in `CalendarEventBuilder`

---

## Controller

`ShiftController` is updated:

- Both `IGoogleCalendarService` and `INextcloudCalendarService` injected. These are empty marker interfaces that extend `ICalendarService`, registered with their concrete types in DI. This avoids DI ambiguity while keeping the shared interface as the contract:
  ```csharp
  public interface IGoogleCalendarService : ICalendarService { }
  public interface INextcloudCalendarService : ICalendarService { }
  ```
- Old endpoints `GET /api/shift/google_calendars` and `POST /api/shift/sync_to_google_calendar` are **replaced** by:
  - `GET /api/shift/calendars?provider=google|nextcloud` → `List<CalendarInfo>`
  - `POST /api/shift/sync_to_calendar` → body: `{ Provider, CalendarId, Year, Month }`
- Provider routing via a `switch` on the `provider` string — no factory abstraction

---

## Client UI

### Buttons

Two separate sync buttons in `Index.razor`:
- **Sync to Google Calendar** (existing, unchanged label)
- **Sync to Nextcloud** (new)

Both buttons are disabled while `_isSyncing` is true.

### State

New field `_syncProvider` (`"google"` or `"nextcloud"`) tracks which provider triggered the current flow.

Existing `_availableCalendars`, `_showCalendarSelector`, and the calendar picker modal are reused for both providers. The modal title changes to "Select Google Calendar" or "Select Nextcloud Calendar" based on `_syncProvider`.

### Client DTO

`GoogleCalendar` record on the client is renamed to `CalendarInfo` to match the server DTO.

---

## Configuration

### `appsettings.json` (placeholder — committed to git)

```json
"Nextcloud": {
  "BaseUrl": "https://your-nextcloud-instance.example.com",
  "Username": "YOUR_NEXTCLOUD_USERNAME",
  "AppPassword": "YOUR_NEXTCLOUD_APP_PASSWORD"
}
```

### Production appsettings (not committed, mounted via Docker)

Contains real values for `BaseUrl`, `Username`, and `AppPassword`.

Read in `Program.cs` via `IConfiguration`, injected into `NextcloudCalendarService` — same pattern as `Authentication:Google`.

### `DOCKER_CONFIG.md`

Add a note that the `Nextcloud` section must be populated in the production `appsettings.json`.

---

## Dependencies

No new NuGet packages required:
- CalDAV HTTP calls: plain `HttpClient`
- iCalendar VEVENT generation: `Ical.Net` (already in `ShiftScheduler.Services.csproj`)
- XML parsing for CalDAV responses: `System.Xml` (built-in)

---

## Out of scope

- Two-way sync (read events from calendar back into the scheduler)
- Nextcloud OAuth — app password is sufficient for a personal instance
- Caching of calendar lists
