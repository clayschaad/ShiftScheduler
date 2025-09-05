# Use the ASP.NET Core runtime as the base image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base

# Install emoji fonts for PDF generation
RUN apt-get update && apt-get install -y \
    fonts-noto-color-emoji \
    fontconfig \
    && rm -rf /var/lib/apt/lists/* \
    && fc-cache -fv

WORKDIR /app
EXPOSE 5000

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY ["Server/ShiftScheduler.Server.csproj", "Server/"]
COPY ["Client/ShiftScheduler.Client.csproj", "Client/"]
COPY ["Shared/ShiftScheduler.Shared.csproj", "Shared/"]
COPY ["Services/ShiftScheduler.Services.csproj", "Services/"]

# Restore dependencies
RUN dotnet restore "Server/ShiftScheduler.Server.csproj"

# Copy everything else
COPY . .

# Build the application
WORKDIR "/src/Server"
RUN dotnet build "ShiftScheduler.Server.csproj" -c Release -o /app/build

# Publish stage  
FROM build AS publish
RUN dotnet publish "ShiftScheduler.Server.csproj" -c Release -o /app/publish

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create config directory for persistent configuration
RUN mkdir -p /app/config

ENTRYPOINT ["dotnet", "ShiftScheduler.Server.dll"]