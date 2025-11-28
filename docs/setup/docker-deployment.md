---
layout: default
title: Docker Deployment Overview
parent: Setup and Installation
nav_order: 2
---

# Docker Deployment Overview

This page summarizes the containerized runtime that ships with LogMyDay. Use it when you need a refresher on the moving parts or want to verify that the host is correctly configured.

## Key Assets

| File | Purpose |
| --- | --- |
| `Dockerfile` | Multi-stage build that compiles Tailwind assets, publishes the .NET app, and runs as a non-root user on port 9099. |
| `docker-compose.yml` | Defines the `logmyday-app` service, health checks, secrets mount, and persistent log volume. |
| `.dockerignore` | Excludes source artifacts, secrets, and mobile projects from the build context. |
| `src/LogMyDay.App/appsettings.Docker.json` | Overrides configuration for the container environment. |
| `docker-config/` | Optional runtime overrides that can be bind-mounted into the container. |
| `secrets/*.txt` | Docker secrets consumed through `/run/secrets/` when running with Compose. |

## Quick Start

```powershell
# 1. Provide runtime values
Copy-Item .env.example .env
# Populate connection strings, SMTP configuration, and rate limit values.

# 2. Provide secrets (kept out of git)
Copy-Item secrets\db_password.txt.example secrets\db_password.txt
Copy-Item secrets\smtp_password.txt.example secrets\smtp_password.txt
# Fill the files with real credentials.

# 3. Build and launch
docker compose up --build -d

# 4. Check container health
Docker ps
docker inspect --format='{{json .State.Health}}' logmyday-app
```

The service exposes port `9099` by default. Add a reverse proxy (nginx, Traefik, Azure App Gateway) to terminate HTTPS in production.

## Secrets & Configuration Flow

1. **Base configuration**: `appsettings.json` baked into the image.
2. **Docker defaults**: `appsettings.Docker.json` adjusts logging and connection strings for containers.
3. **Environment variables**: Provided via `docker-compose.yml` + `.env` file.
4. **Docker secrets**: Mounted as files under `/run/secrets/` and loaded automatically when `ASPNETCORE_ENVIRONMENT=Docker`.

## Maintenance Tips

- Rotate secrets and regenerate the container with `docker compose up --build`.
- Use `docker compose logs -f logmyday-app` for tailing runtime logs stored in the `logmyday-logs` volume.
- Apply OS security updates by rebuilding the image regularly—base images are not auto-patched.
- Keep the `.env` and `secrets/` copies out of version control; templates ending with `.example` are safe to commit.
