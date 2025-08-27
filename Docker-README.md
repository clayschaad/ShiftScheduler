# ShiftScheduler Docker Setup

This directory contains Docker configuration files for running the ShiftScheduler application in containers.

## Files

- `Dockerfile` - Main production dockerfile that uses local source files (recommended)
- `Dockerfile.production` - Alternative dockerfile that attempts to clone from GitHub repository
- `Dockerfile.local` - Development dockerfile that uses local source files
- `docker-compose.yml` - Docker Compose configuration for easy deployment
- `.dockerignore` - Files to exclude from Docker build context

## Usage

### Option 1: Using Local Source (Recommended)

```bash
# Build using local source files - most reliable approach
docker build -t shiftscheduler:latest .

# Run the container
docker run -p 5000:5000 shiftscheduler:latest
```

### Option 2: Using Docker Compose (Easiest)

```bash
# Start the application
docker-compose up --build

# Run in background
docker-compose up -d --build

# Stop the application
docker-compose down
```

### Option 3: Development Build

```bash
# Build for development
docker build -f Dockerfile.local -t shiftscheduler:dev .

# Run the container
docker run -p 5000:5000 shiftscheduler:dev
```

### Option 4: Git Repository Clone (May Fail in Restricted Environments)

```bash
# Build using git repository clone - may fail due to auth/SSL issues
docker build -f Dockerfile.production -t shiftscheduler:production .

# Run the container
docker run -p 5000:5000 shiftscheduler:production
```

## Accessing the Application

Once running, the application will be available at:
- http://localhost:5000

## Notes

- **Main Dockerfile**: Uses local source files for maximum reliability
- **Dockerfile.production**: Attempts to clone from GitHub but may fail in restricted environments
- **Dockerfile.local**: Same as main Dockerfile but with explicit naming for development
- The application runs on port 5000 inside the container
- Environment is set to Production by default in Docker containers

## Troubleshooting

### Git Clone Authentication Issues

If you encounter errors like:
```
fatal: could not read Username for 'https://github.com': No such device or address
```

This is common in Docker build environments with:
- Network restrictions
- SSL certificate verification issues
- Missing authentication credentials

**Solutions:**
1. **Use the main Dockerfile instead**: `docker build -t shiftscheduler .`
2. **Use Docker Compose**: `docker-compose up --build`
3. **Build with local files**: `docker build -f Dockerfile.local -t shiftscheduler .`

### SSL Certificate Issues

If you encounter SSL certificate errors:

1. Use the local source approach: `docker build -t shiftscheduler .`
2. Use Docker Compose: `docker-compose up --build`
3. Build with network host mode: `docker build --network=host -t shiftscheduler .`

### General Docker Issues

- Ensure no other service is using port 5000
- For development, use `docker build -f Dockerfile.local -t shiftscheduler .`
- Check Docker daemon is running and accessible