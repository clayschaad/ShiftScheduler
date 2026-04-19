# Configurable Icons from Docker Share

**Date:** 2026-04-14  
**Status:** Approved

## Problem

Icons for shifts are currently PNG files bundled in the repository under `Client/wwwroot/icons/`. This means adding or changing an icon requires a code change and a redeployment. The user wants icons to be configurable at runtime, placed on the same Docker-mounted volume as monthly schedule files and other configuration.

## Solution

Serve icon files from `config/icons/` (a subfolder of the existing Docker share) using ASP.NET Core's static file middleware with a `PhysicalFileProvider`. No frontend changes are required — the URL path `/icons/filename.png` stays identical.

## Architecture

### Server change (`Server/Program.cs`)

Add a second `UseStaticFiles` call after the existing one:

```csharp
var iconsPath = Path.Combine(Directory.GetCurrentDirectory(), "config", "icons");
Directory.CreateDirectory(iconsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(iconsPath),
    RequestPath = "/icons"
});
```

- `config/icons/` is created automatically on first run if absent.
- Files placed there are served at `/icons/{filename}` immediately, no restart needed.
- Built-in ASP.NET Core static file serving provides ETags, cache headers, and range request support.

### Cleanup

- Delete `Client/wwwroot/icons/` and all PNG files within it.
- Remove all `<Content Update="wwwroot\icons\*.png">` entries from `Client/ShiftScheduler.Client.csproj`.

### No changes needed

- `Shift.IsPngIcon` computed property
- `Shift.Icon` field
- `<img src="icons\@shift.Icon">` in `Index.razor`
- `ConfigurationDialog.razor`
- Any other frontend or shared model code

## Data Flow

1. User places `myicon.png` in `config/icons/` on the Docker share.
2. In `appsettings.json` (or `config/shifts.json`), the shift's `Icon` field is set to `myicon.png`.
3. The Blazor client fetches shift config from `GET /api/shift/shifts`.
4. `Shift.IsPngIcon` returns `true`, and the UI renders `<img src="icons/myicon.png">`.
5. The browser GETs `/icons/myicon.png` — served by the `PhysicalFileProvider` from `config/icons/myicon.png`.

## Out of Scope

- Emoji icons are unaffected.
- No upload UI for icons — files are placed on the share directly (consistent with how schedule files work).
- No fallback to repo-bundled icons (repo icons are removed entirely).
