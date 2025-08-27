# Use the official .NET 9.0 SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-env

# Set working directory
WORKDIR /source

# Copy the project files from current directory
# Note: In production, replace this with git clone or download from repository
# For example: RUN git clone https://github.com/clayschaad/ShiftScheduler.git .
COPY . .

# Configure NuGet to bypass SSL certificate validation for package restore
# This is necessary in Docker environments with certificate trust issues
ENV NUGET_CERT_REVOCATION_MODE=offline
RUN dotnet nuget update source nuget.org --source https://api.nuget.org/v3/index.json --configfile ~/.nuget/NuGet/NuGet.Config || \
    echo "Warning: Could not update NuGet configuration, proceeding with default settings"

# Restore dependencies
RUN dotnet restore

# Build the application
RUN dotnet build --configuration Release --no-restore

# Publish the application
RUN dotnet publish Server/ShiftScheduler.Server.csproj --configuration Release --no-build --output /app/publish

# Use the official .NET 9.0 runtime image for the final stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0

# Set working directory
WORKDIR /app

# Copy the published application from the build stage
COPY --from=build-env /app/publish .

# Expose the HTTP port (5000 is the default from launchSettings.json)
EXPOSE 5000

# Set environment variables for production
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5000

# Run the application
ENTRYPOINT ["dotnet", "ShiftScheduler.Server.dll"]