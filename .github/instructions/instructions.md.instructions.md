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
- `LogMyDay.App/`: Blazor Server application.
  - `Components/`: UI components, layouts, and pages.
  - `Authentication/`: Server-side authentication logic.
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
- **LogMyDay.App**: A Blazor Server application that serves as the main user interface, allowing users to log, view, and manage their daily activities.
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

### Quick Activities System
LogMyDay features an advanced Quick Activities system designed for one-tap activity logging:

#### Modern User Interface
- **Floating Action Button (FAB)**: Consistent with Activities page design, positioned at bottom-right for easy mobile access
- **Bootstrap Modal Form**: Clean, responsive modal with proper form validation replacing complex JavaScript prompts
- **Mobile-Optimized**: Fullscreen modal on small devices, responsive design for all screen sizes
- **Visual Feedback**: Clear indication of button states (enabled/disabled) with appropriate icons and styling

#### Intelligent Button Creation
- **Tag-Based Setup**: Select from available tags with dropdown selection
- **Auto-Naming**: Button name automatically pre-fills with selected tag name for convenience
- **Predefined Values**: Support for predefined description values based on tag input types:
  - **Integer**: Numeric input with proper validation
  - **String**: Text input for custom descriptions
  - **Boolean**: True/False dropdown selection
  - **Date**: Date input for time-based activities
- **Form Validation**: Proper client-side and server-side validation with clear error messages

#### Smart Cooldown System
- **Accidental Prevention**: 15-second cooldown prevents accidental double-taps
- **Visual Indicators**: Disabled buttons show hourglass icon and "Cooling down..." message
- **State Management**: Real-time updates across the interface when cooldown expires
- **User Feedback**: Success messages confirm activity logging and cooldown activation

#### Enhanced Activity Logging
- **Predefined Descriptions**: Uses configured values instead of generic "Quick activity" text
- **Instant Logging**: One-tap creates activities with current timestamp
- **Tag Integration**: Full integration with existing tag system and input types
- **Error Handling**: Comprehensive error handling with user-friendly messages

#### Technical Implementation
- **Type-Safe Properties**: Proper integration with QuickActivityButton model
- **Event-Driven Updates**: Real-time UI updates through service events
- **Modal Management**: Proper Bootstrap modal lifecycle with JavaScript interop
- **Blazor Integration**: Full Blazor component integration with form validation and state management

This system transforms quick activity logging from a complex multi-step process into a streamlined, single-tap experience that users actually want to use for frequent activity tracking.

## Security

### Authentication Architecture
LogMyDay uses a secure authentication system designed to protect user credentials and resist common web vulnerabilities:

#### Credential Storage Security
- **No localStorage Credential Storage**: The application does NOT store plain credentials in browser localStorage, protecting against XSS attacks
- **Server-Side Session Management**: As a Blazor Server application, authentication state is maintained server-side, not in the browser
- **In-Memory Credential Storage**: The `CredentialStore` class stores credentials in server-side memory only during the active session
- **Session-Based Security**: Credentials are automatically cleared when the user logs out or the session ends

#### Implementation Details
- **CredentialStore Service**: Registered as a singleton service that maintains credentials in private memory fields
- **AuthenticationHeaderHandler**: Automatically injects Basic Auth headers for API calls using server-side stored credentials
- **No Client-Side Persistence**: Credentials never reach the browser's localStorage, sessionStorage, or cookies
- **XSS Protection**: Since credentials are stored server-side, they are not accessible to malicious JavaScript

#### Security Benefits
- Resistant to XSS credential theft attacks
- No credential exposure through browser developer tools
- Automatic credential cleanup on session termination
- Server-side authentication state management

#### Critical Security Rule
**NEVER store user credentials in localStorage, sessionStorage, or any client-side storage mechanism.** The current server-side approach must be maintained to ensure security compliance and protect against credential theft.

### HTTPS Enforcement
LogMyDay enforces HTTPS everywhere to protect data in transit:

#### HTTPS Configuration
- **HTTPS Redirection**: All HTTP requests are automatically redirected to HTTPS using `UseHttpsRedirection()`
- **HSTS Headers**: HTTP Strict Transport Security headers force browsers to use HTTPS for all future requests
- **Development HTTPS**: Development environment uses HTTPS-only launch profiles (no HTTP fallback)
- **Production Security**: Enhanced security headers including HSTS, X-Frame-Options, and XSS protection

#### Database Encryption
- **Production**: SQL Server connections use `Encrypt=True` with certificate validation
- **Development**: SQL Server connections use `Encrypt=True` with `TrustServerCertificate=True` for localhost
- **No Unencrypted Communications**: All API and database traffic is encrypted

#### Security Headers
The application automatically adds security headers to all responses:
- `Strict-Transport-Security`: Enforces HTTPS for 1 year including subdomains
- `X-Frame-Options`: Prevents clickjacking attacks
- `X-Content-Type-Options`: Prevents MIME-type sniffing
- `X-XSS-Protection`: Enables browser XSS filtering
- `Referrer-Policy`: Controls referrer information leakage

### Rate Limiting & Brute-Force Protection
LogMyDay implements comprehensive protection against brute-force authentication attacks:

#### Multi-Layer Rate Limiting
- **Global API Rate Limiting**: 100 requests per minute per IP for general API access
- **Authentication Rate Limiting**: 10 authentication attempts per 15 minutes per IP for API endpoints
- **Custom Authentication Tracking**: Progressive lockout system for failed login attempts

#### Authentication Attempt Tracking
- **Granular Tracking**: Monitors failed attempts by IP address and username combination
- **Progressive Lockouts**: Implements increasing delays (1min → 5min → 15min → 30min → 1hour)
- **Automatic Recovery**: Clears tracking on successful authentication or after time windows expire
- **Configurable Thresholds**: Separate settings for development (3 attempts/10min) and production (5 attempts/15min)

#### Implementation Details
- **AuthAttemptTracker Service**: In-memory tracking of authentication failures with automatic cleanup
- **BasicAuthHandler Integration**: Checks for blocks before credential validation, records attempts
- **Comprehensive Logging**: Detailed logging of failed attempts, lockouts, and suspicious activity
- **IP-Based Protection**: Prevents distributed attacks while allowing legitimate users from different IPs

#### Rate Limiting Configuration
Development settings (more lenient for testing):
```json
{
  "Security": {
    "RateLimit": {
      "MaxAttemptsPerWindow": 3,
      "WindowMinutes": 10,
      "LockoutMinutes": 5
    }
  }
}
```

Production settings (stricter security):
```json
{
  "Security": {
    "RateLimit": {
      "MaxAttemptsPerWindow": 5,
      "WindowMinutes": 15,
      "LockoutMinutes": 30
    }
  }
}
```

## Architecture Guidance for Copilot

* Use **Clean Architecture principles**:

  * Separate concerns into clear layers (Domain, Application, Infrastructure, UI).
  * Avoid code-behind logic; prefer services and dependency injection.
  * Keep domain models free of dependencies on infrastructure or UI.

* Ensure all code aligns with **Context7 MCP** (Modular Clean Project) structure:

  * Follow modular, scalable folder conventions and patterns.
  * Use feature folders and domain-driven structure as applicable.

* Integrate **SequentialThinking MCP** for progressive planning:

  * Prefer planning-first mindset before coding.
  * Write journal notes and intent definitions before actual implementation.

### Usage Notes for Copilot

* Use this document as reference before generating code.
* Do not implement "Planned" or "Deferred" features until promoted.
* Use Developer Journal to explain changes.
* Log errors or tech limitations here to reduce retries.

## Rules and conventions

### Documentation Standards
- **CRITICAL: ALL markdown files must be placed ONLY in the `.github/instructions/` folder** - NEVER create markdown files in the root directory
- **NO EXCEPTIONS**: Documentation, guides, summaries, and all .md files belong in `.github/instructions/` folder only
- New documentation should follow the existing naming convention (lowercase with hyphens)
- Update the main README.md index when adding new documentation files
- Reference documentation files using relative paths from the instructions folder
- **REMINDER**: If you create any .md file outside of `.github/instructions/`, it violates project structure rules

### Code Standards
- Never use Console.WriteLine, Debug.WriteLine, or similar methods for logging. Use the built-in logging framework provided by ASP.NET Core.
- Always use dependency injection for services and repositories.
- Follow the SOLID principles for object-oriented design.
- Use asynchronous programming patterns (async/await) for I/O-bound operations to improve performance and responsiveness.
- Do not use Async in the method names, only when there is also synchronous method of the same name
- Do not use try catch in service method unless really needed (cryptography where it is supposed to and so on)
- Style: Return and throw always have one free line over it
- Prefer braces ({}) for all control structures, including if, for, while, etc., even for single-line statements. This improves readability and reduces the risk of bugs
- Ternary expressions are allowed when they improve clarity, such as for simple conditional assignments or return values
- **Security**: Never store user credentials in localStorage, sessionStorage, or any client-side storage. Maintain the current server-side credential management approach to ensure security compliance.
- **HTTPS Enforcement**: Always use HTTPS for all communications. Never add HTTP-only launch profiles or disable SQL Server encryption. All data in transit must be encrypted.
- **Rate Limiting**: Maintain rate limiting and brute-force protection. Never disable authentication attempt tracking or remove progressive lockout mechanisms. All authentication failures must be logged and tracked.

## Development and Testing Guidelines

### Server Management
- **Always stop the development server at the end of each conversation/session** to prevent port conflicts and file locking issues
- When testing changes, stop any existing `dotnet run` processes before starting new ones
- If you encounter file locking errors (MSB3026) during build, it usually means:
  - A development server is still running from a previous session
  - Visual Studio has the project open and running
  - Multiple instances of the application are running simultaneously

### Resolution Steps for File Locking Issues
1. Stop all `dotnet run` processes in terminals
2. Close any running instances in Visual Studio debugger
3. Use `Stop-Process -Name "dotnet" -Force` if needed to kill all dotnet processes
4. Wait a few seconds before restarting the application

### Testing Workflow
1. Start the server with `dotnet run` for testing
2. Test the changes in the browser
3. **Always terminate the server process before ending the session**
4. Document any server management steps taken during development

### Build and Run Commands
```bash
# Build the solution
dotnet build LogMyDay.sln

# Run the API project
dotnet run --project LogMyDay.Api

# Run the web application
dotnet run --project LogMyDay.App

# Stop running processes
# Use Ctrl+C in terminal or Stop-Process command
```