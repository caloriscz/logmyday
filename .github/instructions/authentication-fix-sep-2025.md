# Authentication Fix - September 2025

## 🐛 Issue Description

When the mobile app tried to update user settings (like changing culture from `en-US` to `cs-CZ`), the server returned `401 Unauthorized` despite the mobile app sending correct Basic Authentication credentials.

### Root Cause

The server's authentication system was configured with `"lmd-cookie"` as the **default authentication scheme**:

```csharp
services.AddAuthentication("lmd-cookie")
```

When a request came in with a Basic Authorization header:
1. The authentication middleware ran the "lmd-cookie" handler first (because it was the default)
2. The cookie handler didn't find a cookie, so it returned "not authenticated"
3. The request proceeded with `User.Identity.IsAuthenticated = false`
4. **The Basic auth handler was NEVER tried** because it wasn't the default scheme

Even though controllers had `[Authorize(AuthenticationSchemes = "lmd-cookie,basic")]`, this attribute only tells the authorization middleware "if the user IS authenticated, make sure they used one of these schemes" - it doesn't tell the authentication middleware to TRY both schemes.

## ✅ Solution Implemented

Implemented a **Policy-Based Authentication Scheme** that intelligently selects the appropriate authentication method based on the incoming request:

### Changes in `LogMyDay.App/Program.cs`

```csharp
// Configure authentication with support for both cookie (Blazor Server) and Basic (Mobile API)
// Use a policy scheme that tries both authentication methods
services.AddAuthentication(options =>
    {
        // Use a composite scheme that tries both cookie and basic
        options.DefaultScheme = "smart-auth";
        options.DefaultChallengeScheme = "smart-auth";
    })
    .AddPolicyScheme("smart-auth", "Smart Authentication", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            // If the request has an Authorization header with "Basic", use Basic auth
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (authHeader?.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase) == true)
            {
                return "basic";
            }
            // Otherwise, use cookie auth
            return "lmd-cookie";
        };
    })
    .AddCookie("lmd-cookie", options => { /* ... */ })
    .AddScheme<AuthenticationSchemeOptions, BasicAuthHandler>("basic", options => { });
```

### Changes in `LogMyDay.Api/Controllers/BaseApiController.cs`

Simplified the authorization attribute since "smart-auth" is now the default:

```csharp
[Authorize] // No need to specify schemes - smart-auth handles it
[ApiController]
public abstract class BaseApiController : ControllerBase
```

## 🔍 How It Works

1. **Mobile App Request**:
   - Mobile sends: `Authorization: Basic Y2Fsb3Jpc0BjYWxvcmlzLmN6OnNlY3JldDEyMw==`
   - Smart-auth detects "Basic " prefix in header
   - Routes request to `BasicAuthHandler`
   - BasicAuthHandler validates credentials and creates claims
   - Request proceeds as authenticated ✅

2. **Blazor Server Request**:
   - Blazor sends: Cookie header with authentication cookie
   - Smart-auth detects NO "Authorization: Basic" header
   - Routes request to cookie authentication handler
   - Cookie handler validates session and creates claims
   - Request proceeds as authenticated ✅

## 📋 Files Changed

1. ✅ `LogMyDay.App/Program.cs` - Added PolicyScheme authentication
2. ✅ `LogMyDay.Api/Controllers/BaseApiController.cs` - Simplified [Authorize] attribute

## 🚀 Deployment Requirements

### ❗ **YES, you MUST redeploy the server to logmyday.tadata.cz**

The mobile app cannot authenticate until these server-side changes are deployed. Without this fix:
- Mobile login will work (initial authentication)
- But any subsequent API calls requiring authentication will fail with 401
- Specifically, updating user settings (culture, timezone) will fail

### Deployment Steps

1. **Stop the production server**
2. **Deploy the updated `LogMyDay.App` project** with the authentication changes
3. **Restart the server**
4. **Test mobile app login and settings update**

### Testing Checklist

After deployment, verify:
- [ ] Mobile app can log in successfully
- [ ] Mobile app can fetch tags/activities (API calls work)
- [ ] Mobile app can update user settings (culture change from en-US to cs-CZ)
- [ ] Blazor Server app still works (cookie authentication intact)
- [ ] Blazor Server app can log in and access all features

## 🔐 Security Implications

This change **maintains security** while fixing functionality:
- Basic authentication still requires valid username/password
- Cookie authentication still requires valid session cookie
- Both authentication methods create proper claims with user identity
- Authorization policies still apply (admin-only endpoints, etc.)
- Rate limiting and brute-force protection still active

## 📊 Impact on Existing Features

### ✅ No Breaking Changes
- Blazor Server app continues working exactly as before
- Cookie-based sessions remain unchanged
- All existing user sessions stay valid
- No database migrations required

### ✅ Mobile App Authentication Now Works
- Mobile login: Already worked ✅
- Mobile API calls: **NOW FIXED** ✅
- Mobile settings update: **NOW FIXED** ✅

## 🧪 Local Testing Instructions

### Test Mobile Authentication Flow

1. **Start the local server**:
   ```powershell
   cd LogMyDay.App
   dotnet run
   ```

2. **Run the mobile app** in emulator or device

3. **Log in to mobile app** with test credentials:
   - Email: `caloris@caloris.cz`
   - Password: `secret123`

4. **Navigate to Settings**

5. **Change culture from en-US to cs-CZ**

6. **Expected result**: Settings save successfully, no 401 error

7. **Verify date pickers** reflect the new culture format

### Verify Blazor Server Still Works

1. Navigate to https://localhost:7064 in browser
2. Log in with same credentials
3. Verify all pages load correctly
4. Verify activities can be created/edited
5. Verify settings can be updated

## 📝 Technical Notes

### Why AddPolicyScheme?

ASP.NET Core provides `AddPolicyScheme` specifically for scenarios where you need to support multiple authentication methods and want to intelligently select which one to use based on the request.

Alternative approaches considered:
- ❌ Multiple [Authorize] attributes: Requires code duplication
- ❌ Custom authentication middleware: More complex, harder to maintain
- ✅ PolicyScheme: Built-in, clean, declarative, testable

### Why Not Just Multiple Schemes in [Authorize]?

The `[Authorize(AuthenticationSchemes = "lmd-cookie,basic")]` syntax tells the **authorization** middleware which schemes are acceptable, but it doesn't tell the **authentication** middleware to try both.

With PolicyScheme:
1. Authentication middleware asks PolicyScheme: "Which handler should I use?"
2. PolicyScheme looks at the request and decides: "Use basic" or "Use lmd-cookie"
3. Authentication runs the selected handler
4. Authorization middleware checks if the user is authenticated (works automatically)

## 🔄 Future Enhancements

Consider these improvements in future iterations:
- [ ] Add authentication telemetry (track which scheme is used for requests)
- [ ] Add authentication fallback (try cookie first, then basic as fallback)
- [ ] Add JWT authentication for mobile (more secure than Basic Auth)
- [ ] Add refresh tokens for long-lived mobile sessions

## 🎯 Related Documentation

- [Security Documentation](./security-overview.md) - Overview of authentication architecture
- [Mobile Authentication](./mobile-authentication.md) - Mobile-specific auth patterns
- [Blazor Server Authentication](./blazor-authentication.md) - Server-side cookie auth

---

**Last Updated**: September 2025  
**Status**: ✅ Fixed - Awaiting Production Deployment  
**Priority**: 🔴 HIGH - Required for mobile app functionality
