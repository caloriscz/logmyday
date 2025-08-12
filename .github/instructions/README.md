# LogMyDay Documentation

This folder contains all project documentation and implementation guides.

## Project Overview

LogMyDay is a personal activity logging application with both web and mobile interfaces.

## Documentation Index

### Main Project Files
- **[Project Instructions](instructions.md.instructions.md)** - Core project structure, architecture guidelines, and development rules

### Mobile Application
- **[Mobile Implementation](mobile-implementation.md)** - Mobile app development journey and key features
- **[Mobile Bug Fixes](mobile-bug-fixes.md)** - Bug resolution and troubleshooting guide

### Implementation Guides
- **[Implementation Summary](implementation-summary.md)** - High-level overview of completed features
- **[Backup Documentation](backup-documentation.md)** - Data backup and restoration system

### Debugging and Development
- **[On-Screen Debug Guide](on-screen-debug-guide.md)** - Debugging techniques for UI issues
- **[Button Debug Guide](button-debug-guide.md)** - Specific guide for button functionality debugging
- **[API Debug Enhancements](api-debug-enhancements.md)** - API connectivity and authentication debugging

## Project Structure

- **LogMyDay.Api** - ASP.NET Core Web API backend
- **LogMyDay.App** - Blazor Server web application  
- **LogMyDay.App.Mobile** - .NET MAUI cross-platform mobile app
- **LogMyDay.Domain** - Core domain models and business logic
- **LogMyDay.Shared** - Shared DTOs and interfaces
- **LogMyDay.Api.Tests** - Unit tests for API and services

## Key Features

- **Activity Logging** - Track daily activities with tags and timestamps
- **Tag Management** - Categorize activities with custom tags and input types
- **Quick Activities** - Mobile-optimized buttons for frequent activities (15-second cooldown)
- **Data Backup** - Export/import functionality for data portability
- **Authentication** - Secure login with rate limiting and brute-force protection
- **Responsive Design** - Optimized for both desktop and mobile devices

## Development Guidelines

- Follow Clean Architecture principles
- Use dependency injection and SOLID principles
- Implement proper error handling and logging
- Maintain security best practices (HTTPS, secure credential storage)
- Write unit tests for critical functionality

## Security Notes

- Never store credentials in client-side storage
- Always use HTTPS for all communications
- Implement rate limiting for authentication endpoints
- Follow proper authentication and authorization patterns

---

For the most up-to-date project instructions and coding guidelines, see [instructions.md.instructions.md](instructions.md.instructions.md).
