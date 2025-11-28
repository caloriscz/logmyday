# Multi-stage Dockerfile for LogMyDay.App
# Pre-production quality for testing before public release

# Stage 1: Build Tailwind CSS
FROM node:20-alpine AS tailwind-builder
WORKDIR /app
# Copy Razor/HTML files that Tailwind needs to scan for CSS classes
COPY src/LogMyDay.App/ ./LogMyDay.App/
COPY src/LogMyDay.UI/ ./LogMyDay.UI/
# Copy Tailwind config and source files
COPY src/ui/ ./ui/
WORKDIR /app/ui
RUN npm ci
RUN npm run build

# Stage 2: Build .NET application
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files for restore (leverage Docker cache)
COPY ["src/LogMyDay.App/LogMyDay.App.csproj", "LogMyDay.App/"]
COPY ["src/LogMyDay.Api/LogMyDay.Api.csproj", "LogMyDay.Api/"]
COPY ["src/LogMyDay.Domain/LogMyDay.Domain.csproj", "LogMyDay.Domain/"]
COPY ["src/LogMyDay.Shared/LogMyDay.Shared.csproj", "LogMyDay.Shared/"]
COPY ["src/LogMyDay.UI/LogMyDay.UI.csproj", "LogMyDay.UI/"]

# Restore dependencies
RUN dotnet restore "LogMyDay.App/LogMyDay.App.csproj"

# Copy all source code
COPY . .

# Copy built Tailwind assets from previous stage
COPY --from=tailwind-builder /app/ui/dist/css/ /src/LogMyDay.App/wwwroot/css/
COPY --from=tailwind-builder /app/ui/dist/js/ /src/LogMyDay.App/wwwroot/js/
COPY --from=tailwind-builder /app/ui/dist/vendor/ /src/LogMyDay.App/wwwroot/vendor/

# Build application (skip Tailwind build target since we already built it)
WORKDIR /src/LogMyDay.App
RUN dotnet build "LogMyDay.App.csproj" -c Release -o /app/build -p:SkipBuildTailwindCSS=true

# Publish application
FROM build AS publish
RUN dotnet publish "LogMyDay.App.csproj" -c Release -o /app/publish /p:UseAppHost=false -p:SkipBuildTailwindCSS=true

# Stage 3: Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Create non-root user for security
RUN groupadd -r logmyday && useradd -r -g logmyday logmyday

# Create directories for logs and secrets
RUN mkdir -p /app/Logs /run/secrets && \
    chown -R logmyday:logmyday /app

# Copy published application
COPY --from=publish /app/publish .

# Switch to non-root user
USER logmyday

# Expose port 9099
EXPOSE 9099

# Set environment for Docker
ENV ASPNETCORE_ENVIRONMENT=Docker
ENV ASPNETCORE_URLS=http://+:9099

# Health check endpoint
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:9099/health || exit 1

ENTRYPOINT ["dotnet", "LogMyDay.App.dll"]
