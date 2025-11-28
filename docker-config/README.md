# Docker Configuration Directory

This directory contains Docker-specific configuration overrides that are mounted into containers at runtime.

## Files

### appsettings.Docker.json
Optional runtime configuration overrides for Docker environment. This file is mounted as read-only at `/app/appsettings.Docker.json` inside the container.

**When to use:**
- Environment-specific logging configuration
- Custom connection string patterns
- Runtime feature toggles
- Development/staging/production variations

**Example use case:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  }
}
```

## Usage

The docker-compose.yml mounts this directory's appsettings.Docker.json file:

```yaml
volumes:
  - ./docker-config/appsettings.Docker.json:/app/appsettings.Docker.json:ro
```

**Note:** The appsettings.Docker.json file in src/LogMyDay.App/ directory is baked into the Docker image at build time and provides base Docker configuration. This directory allows runtime overrides without rebuilding the image.

## Best Practices

1. **Version Control**: Commit template/example files, not sensitive data
2. **Read-Only Mounts**: Use `:ro` flag for configuration files
3. **Environment Variables**: Prefer environment variables over config file overrides when possible
4. **Documentation**: Document any custom overrides with comments in JSON files

## Git Ignore

Non-sensitive configuration files in this directory should be committed. Add to `.gitignore` if you need to store sensitive overrides:

```
docker-config/appsettings.Production.json
docker-config/*.sensitive.json
```
