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

## Basic Project Information

LogMyDay is a personal activity logging application. User can add tags to activities, manage backups, and view their logged activities. 

The application is designed to be user-friendly and efficient for tracking daily activities.

It consists of:

- **LogMyDay.Api**: An ASP.NET Core Web API that provides endpoints for managing activities, tags, backups, and more. It handles authentication and data storage.
- **LogMyDay.App**: A Blazor Server app client that serves as the main user interface, allowing users to log, view, and manage their daily activities.
- **LogMyDay.Domain**: Contains the core domain models and business logic, including entities and enums used throughout the application.
- **LogMyDay.Shared**: Defines shared data transfer objects (DTOs) and interfaces for communication between the client and API.
- **LogMyDay.Api.Tests**: Contains unit tests for the API and service layers to ensure reliability and correctness.

The project is designed for extensibility and separation of concerns, making it easy to maintain and expand.