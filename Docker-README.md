# ShiftScheduler Docker Setup

This directory contains Docker configuration files for running the ShiftScheduler application in containers.

## Files

- `Dockerfile` - Production dockerfile that clones the repository from GitHub
- `Dockerfile.local` - Development dockerfile that uses local source files
- `docker-compose.yml` - Docker Compose configuration for easy deployment
- `.dockerignore` - Files to exclude from Docker build context

## Usage

### Option 1: Using Local Source (Recommended for Development)

```bash
# Build using local source files
docker build -f Dockerfile.local -t shiftscheduler:local .

# Run the container
docker run -p 5000:5000 shiftscheduler:local
```

### Option 2: Using Git Repository Clone (Production)

```bash
# Build using git repository clone
docker build -t shiftscheduler:latest .

# Run the container
docker run -p 5000:5000 shiftscheduler:latest
```

### Option 3: Using Docker Compose (Easiest)

```bash
# Start the application
docker-compose up --build

# Run in background
docker-compose up -d --build

# Stop the application
docker-compose down
```

## Accessing the Application

Once running, the application will be available at:
- http://localhost:5000

## Notes

- The production Dockerfile clones the latest version from the GitHub repository
- The local Dockerfile uses the current source files for development
- The application runs on port 5000 inside the container
- Environment is set to Production by default in Docker containers

## Troubleshooting

If you encounter SSL certificate issues during build, you can:

1. Use the local Dockerfile instead: `docker build -f Dockerfile.local -t shiftscheduler .`
2. Build with network host mode: `docker build --network=host -t shiftscheduler .`
3. Configure Docker to use system certificates if needed