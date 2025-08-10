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
