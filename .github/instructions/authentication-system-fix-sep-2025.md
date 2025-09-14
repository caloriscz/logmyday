# Authentication System Fix - September 2025

## Problem Description

The LogMyDay Blazor Server application had a critical authentication issue where users would successfully log in but immediately get redirected back to the login page without any error message. This created an authentication loop that prevented users from accessing the application.

## Root Cause Analysis

Through extensive debugging and logging analysis, the root cause was identified as:

**HttpClient instances used by Refit clients were not forwarding authentication cookies from the current HttpContext to API requests**, causing all authenticated API calls to return 401 Unauthorized even after successful login.

### Technical Details

1. **Cookie Authentication Working**: The ASP.NET Core cookie authentication ("lmd-cookie" scheme) was working correctly - users could authenticate and cookies were set properly.

2. **API Calls Failing**: However, when Blazor components made API calls through Refit clients (`IActivityApi`, `IAuthApi`, `IUsersApi`, `IAccountApi`), these HttpClient instances did not include the authentication cookies from the current user session.

3. **Authentication State Mismatch**: This created a situation where:
   - Blazor Server believed the user was authenticated (cookies present in HttpContext)
   - API calls appeared unauthenticated (no cookies forwarded to API requests)
   - MainLayout authentication checks would fail and redirect to login

## Solution Implemented

### 1. Created CookieAuthenticationHandler

**File**: `LogMyDay.App\Authentication\CookieAuthenticationHandler.cs`

```csharp
public sealed class CookieAuthenticationHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CookieAuthenticationHandler> _logger;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        
        if (httpContext != null)
        {
            // Forward authentication cookies from the current request to the API call
            var cookieHeader = httpContext.Request.Headers["Cookie"].FirstOrDefault();
            
            if (!string.IsNullOrEmpty(cookieHeader))
            {
                request.Headers.Add("Cookie", cookieHeader);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
```

This DelegatingHandler automatically forwards authentication cookies from the current HttpContext to all outgoing HTTP requests made by Refit clients.

### 2. Updated Program.cs Configuration

**Changes Made**:

1. **Registered the Handler**:
   ```csharp
   services.AddScoped<CookieAuthenticationHandler>();
   ```

2. **Added Handler to All Refit Clients**:
   ```csharp
   services.AddRefitClient<IActivityApi>()
       .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseAddress))
       .AddHttpMessageHandler<CookieAuthenticationHandler>();

   services.AddRefitClient<IAuthApi>()
       .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseAddress))
       .AddHttpMessageHandler<CookieAuthenticationHandler>();
   
   // ... same for IUsersApi and IAccountApi
   ```

3. **Added Required Using Statement**:
   ```csharp
   using LogMyDay.App.Components;
   ```

### 3. Fixed Backup & Restore User ID Bug

- **Problem**: The backup restore process was assigning a `null` UserID to restored entities (`Activities`, `Tags`), breaking multi-user data separation.
- **Root Cause**: The Blazor UI was calling the `BackupService` directly via dependency injection, bypassing the HTTP pipeline that provides the authenticated user's context. The service call was missing the `userId`.
- **Solution**: Modified the `Backup.razor` component to inject `IAuthApi`, retrieve the current authenticated user's ID, and pass it to the `ImportDataAsync` service method. This ensures all restored data is correctly associated with the user.
- **Impact**: Backup and restore functionality now correctly handles user-specific data scoping.

## Architecture Pattern

This solution follows Microsoft's documented pattern for Blazor Server applications that need to forward authentication context to HTTP clients:

1. **IHttpContextAccessor**: Provides access to the current HttpContext in DI scenarios
2. **DelegatingHandler**: Intercepts HTTP requests and modifies them before sending
3. **Cookie Forwarding**: Automatically includes authentication cookies in API requests

## Security Considerations

✅ **Maintains Security**: 
- No credentials stored in browser storage
- Authentication state remains server-side only
- Cookies automatically expire with session

✅ **Follows Best Practices**:
- Uses ASP.NET Core cookie authentication
- Leverages DelegatingHandler pattern
- Integrates with existing security infrastructure

## Testing Results

- ✅ **Authentication Flow**: Login works without redirect loops
- ✅ **API Integration**: All Refit clients automatically include authentication cookies
- ✅ **Session Management**: Logout clears cookies and redirects properly
- ✅ **Security Headers**: HTTPS enforcement and security headers maintained

## Migration Notes

### Deprecated Components (Blazor Server)
- `AuthenticationHeaderHandler` (Basic Auth variant): No longer needed
- Basic Auth pattern: Replaced with cookie authentication

### Future Cleanup Tasks
- Remove old `CredentialStore` references if no longer used
- Clean up any remaining Basic Auth logic in Blazor Server
- Verify all authentication flows work with cookie-based system

## Impact on Mobile App

**Important**: This fix only affects the Blazor Server application (`LogMyDay.App`). The MAUI mobile application (`LogMyDay.App.Mobile`) still uses the existing Basic Auth pattern with `ApiContext` + `DynamicAuthHandler` + `ApiClientProvider` and **requires separate review and testing** to ensure compatibility.

## Lessons Learned

1. **HttpClient Context Isolation**: HttpClient instances in DI containers don't automatically inherit authentication context from the current request
2. **Cookie vs Header Auth**: Different authentication schemes require different forwarding mechanisms
3. **DelegatingHandler Power**: DelegatingHandlers provide a clean way to modify HTTP requests across all clients
4. **Testing Authentication**: Comprehensive logging is essential for debugging authentication flows

## Next Steps

1. **Mobile App Review**: Test mobile app authentication against the updated Blazor Server
2. **Cleanup**: Remove deprecated authentication components
3. **Documentation**: Update API documentation to reflect cookie authentication
4. **Monitoring**: Monitor logs for any authentication-related issues in production

---
*Fix completed: September 13, 2025*
*Tested and verified working*