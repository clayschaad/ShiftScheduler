# Configuration Persistence for Docker

ShiftScheduler now supports persisting configuration changes in external files, making it suitable for Docker deployments where configuration should survive container rebuilds.

## How it Works

Configuration is automatically saved to and loaded from external JSON files in the `config/` directory:

- `config/shifts.json` - Contains all shift definitions (name, icon, times)
- `config/transport.json` - Contains transport API settings and parameters

## Behavior

1. **Startup**: Service reads from external files if they exist, otherwise uses `appsettings.json` defaults
2. **Initial Creation**: If external files don't exist, they're created with current configuration
3. **Updates**: Any configuration changes via the UI are automatically saved to external files
4. **Persistence**: External files persist across application restarts

## Docker Usage

### Docker Compose Example

```yaml
version: '3.8'
services:
  shiftscheduler:
    build: .
    ports:
      - "5000:5000"
    volumes:
      - ./config:/app/config  # Mount config directory
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
```

### Folder Structure

```
project/
├── docker-compose.yml
├── config/                 # This directory will be created automatically
│   ├── shifts.json        # Persisted shift configurations
│   └── transport.json     # Persisted transport settings
└── ...
```

## Benefits

- **Persistence**: Configuration survives container rebuilds
- **Backup**: Easy to backup/restore configuration by copying JSON files
- **Version Control**: Configuration files can be committed to source control
- **Multiple Environments**: Different config files for different deployments
- **No Data Loss**: UI changes are automatically preserved

## Migration

No migration needed - existing installations will automatically:
1. Create the `config/` directory on next startup
2. Export current configuration to external files
3. Continue using existing settings

## File Format

Configuration files use standard JSON format and can be edited directly if needed. Changes take effect after application restart.

## PDF Export with Emoji Support

The Docker image includes Noto Color Emoji fonts to ensure proper rendering of emoji characters (🚂, 🌅, 🌆) in PDF exports. This addresses issues where emojis appear as empty boxes or don't render at all in containerized environments.

### Troubleshooting PDF Emoji Issues

If emojis still don't display correctly in PDFs:

1. Ensure the container has emoji fonts installed:
   ```bash
   docker exec <container-name> fc-list | grep -i emoji
   ```

2. The fallback text format includes both emoji and descriptive text:
   - `🌅 (Morning) 06:00-14:00`
   - `🌆 (Afternoon) 14:00-22:00`
   - `🚂 (Train) 07:30→08:15`

3. For custom deployments, install emoji fonts manually:
   ```dockerfile
   RUN apt-get update && apt-get install -y fonts-noto-color-emoji fontconfig \
       && fc-cache -fv
   ```