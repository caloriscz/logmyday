---
applyTo: '**'
---

## Project Structure Overview

This repository is organized as follows:

- `LogMyDay.sln`: Solution file for the project.
- `LogMyDay.Api/`: ASP.NET Core Web API project.
  - `Controllers/`: API controllers for activities, backup, tags, etc.
  - `Authentication/`: Basic authentication handlers and options.
  - `Application/`: Application layer interfaces and services.
  - `Infrastructure/`: Data access and infrastructure code.
- `LogMyDay.App/`: Blazor WebAssembly client application.
  - `Components/`: UI components, layouts, and pages.
  - `Authentication/`: Client-side authentication logic.
  - `wwwroot/`: Static web assets (CSS, images, JS libraries).
- `LogMyDay.Domain/`: Domain layer with entities and enums.
  - `Entities/`: Core domain models (Activity, Tag, etc.).
  - `Enums/`: Domain-specific enums.
- `LogMyDay.Shared/`: Shared DTOs and interfaces for API communication.
- `LogMyDay.Api.Tests/`: Unit tests for the API and services.
- `README.md`, `LICENSE`, etc.: Documentation and legal files.