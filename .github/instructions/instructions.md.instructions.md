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

## Tag Entity Overview

The `Tag` entity is a core part of LogMyDay and is used to categorize and structure activity data. Tags can:
- Be marked as **required**: If a tag is required and the user does not provide information for it (such as time granularity), the system will notify the user (planned feature).
- Specify **time granularity**: This determines how often an event can be repeated (e.g., exact time, daily, weekly, etc.).
- Be **repeatable** or not, and can represent a **range** of values.
- Be linked to an **input type** (see below) and a **pattern** for advanced data validation or suggestions.
- Be associated with a specific user (for user-specific tags).

## Input Type Overview

Each tag can have an associated input type, which defines the kind of data the tag accepts:
- **Integer**: Numeric input (e.g., quantity, count).
- **String**: Text input.
- **Boolean**: True/false values.
- **Date**: Date/time input.

Input types help validate user data and improve the user experience. For example, an integer input type will display a numeric input field in the UI, making data entry easier and more reliable.

## Key Features

### Dynamic Input Types
The application automatically renders different input controls based on the selected tag's input type:
- **Integer tags**: Display a `<input type="number">` field with numeric keyboard support on mobile devices
- **String tags**: Display a text input field for free-form text entry
- **Boolean tags**: Display a checkbox for true/false values
- **Date tags**: Display a date picker for date selection
- **Default/Other types**: Fall back to a standard text input field

This dynamic rendering improves user experience by providing appropriate input controls and mobile-optimized keyboards for different data types.

### Smart Date Initialization
When creating new activities, the application intelligently sets the initial date and time:
- **Today's activities**: If the selected date is today, the DateStarted field is initialized to the current date and time (`DateTime.Now`)
- **Past/future activities**: If the selected date is not today, the DateStarted field is initialized to the beginning of the selected day (midnight)
- **Calendar navigation**: When navigating between dates using the calendar or date navigation controls, new activities automatically inherit the selected date

This feature ensures that activity timestamps are contextually appropriate and reduces the need for manual date/time adjustments when logging activities.

### Enhanced Daily Navigation
The daily view includes multiple navigation options for efficient access to historical data:
- **Day Navigation**: Standard previous/next arrows (`<`, `>`) for moving one day at a time
- **Week Navigation**: Week arrows (`<<`, `>>`) for jumping 7 days forward or backward
- **Month Navigation**: Month arrows (`<<<`, `>>>`) for navigating by full calendar months
- **Smart Disable Logic**: Navigation buttons are automatically disabled when reaching limits (5-year history boundary, future dates)
- **Mobile Optimized**: All navigation controls are touch-friendly and responsive across devices

This multi-level navigation system eliminates the tedious day-by-day clicking when accessing activities from weeks or months ago, significantly improving the user experience for historical data review.

## Rules and conventions

- Never use Console.WriteLine, Debug.WriteLine, or similar methods for logging. Use the built-in logging framework provided by ASP.NET Core.
- Always use dependency injection for services and repositories.
- Follow the SOLID principles for object-oriented design.
- Use asynchronous programming patterns (async/await) for I/O-bound operations to improve performance and responsiveness.
- Do not use Async in the method names, only when there is also synchronous method of the same name
- Do not use try catch in service method unless really needed (cryptography where it is supposed to and so on)
- Style: Return and throw always have one free line over it
- Prefer braces ({}) for all control structures, including if, for, while, etc., even for single-line statements. This improves readability and reduces the risk of bugs
- Ternary expressions are allowed when they improve clarity, such as for simple conditional assignments or return values

## Development and Testing Guidelines

### Server Management
- **Always stop the development server at the end of each conversation/session** to prevent port conflicts and file locking issues
- When testing changes, stop any existing `dotnet run` processes before starting new ones
- If you encounter file locking errors (MSB3026) during build, it usually means:
  - A development server is still running from a previous session
  - Visual Studio has the project open and running
  - Multiple instances of the application are running simultaneously
- **Resolution steps for file locking issues**:
  1. Stop all `dotnet run` processes in terminals
  2. Close any running instances in Visual Studio debugger
  3. Use `Stop-Process -Name "dotnet" -Force` if needed to kill all dotnet processes
  4. Wait a few seconds before restarting the application

### Testing Workflow
- Start the server with `dotnet run` for testing
- Test the changes in the browser
- **Always terminate the server process before ending the session**
- Document any server management steps taken during development