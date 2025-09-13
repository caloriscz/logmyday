# LogMyDay

Track daily activities with both web and mobile applications.

## Projects

- **LogMyDay.App**: Blazor Server web application for comprehensive activity management
- **LogMyDay.Api**: ASP.NET Core Web API providing backend services
- **LogMyDay.App.Mobile**: .NET MAUI mobile application with Quick Activities feature
- **LogMyDay.Domain**: Core domain models and business logic
- **LogMyDay.Shared**: Shared DTOs and interfaces

## Features

### Web Application (LogMyDay.App)
- Comprehensive activity logging and management
- Tag-based categorization with input types
- Calendar views (daily, weekly, monthly)
- Advanced filtering and search
- Excel export functionality
- Backup and restore capabilities

### Mobile Application (LogMyDay.App.Mobile)
- Quick access to the web application via WebView
- **Quick Activities**: Create buttons for instant activity logging
- 15-second cooldown to prevent accidental double-clicks
- Local storage of quick activity configurations
- Bottom navigation for easy access

## Migration

Run from the root of your solution (or in LogMyDay.App folder):

```
dotnet ef migrations add InitialCreate --project LogMyDay.Api --startup-project LogMyDay.App --output-dir Infrastructure/Data/Migrations
```

Then apply the migration:

```
dotnet ef database update --project LogMyDay.Api --startup-project LogMyDay.App
```

## Generate migration script

```
dotnet ef migrations script --project LogMyDay.Api --startup-project LogMyDay.App --output InitialCreate.sql
```

## Documentation

Comprehensive documentation is available in the `.github/instructions/` folder:

- **[Project Instructions](.github/instructions/instructions.md.instructions.md)**: Complete project structure, architecture, and development guidelines
- **[Authentication System Fix](.github/instructions/authentication-system-fix-sep-2025.md)**: Details about the September 2025 authentication system fix for Blazor Server
- **Security Architecture**: Cookie-based authentication for Blazor Server, Basic Auth for mobile
- **Development Guidelines**: Build processes, testing workflows, and coding standards

## Recent Updates

### September 2025 - Authentication System Fix
- **Fixed**: Blazor Server authentication loop issue where login would redirect back to login page
- **Solution**: Implemented `CookieAuthenticationHandler` to forward authentication cookies to API requests
- **Impact**: Main authentication functionality now works correctly for web application
- **Status**: Mobile app requires review for compatibility (next conversation)
