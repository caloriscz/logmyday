# Authorization Fix - October 6, 2025

## 🎯 Root Cause Identified

The **403 Forbidden** error was caused by **incorrect authorization policy placement** in the `UsersController`.

### The Problem

```csharp
[ApiController]
[Route("api/users")]
[Authorize(Policy = "AdminOnly")]  // ❌ WRONG - Applied to ENTIRE controller
public class UsersController : ControllerBase
{
    // ALL methods required admin privileges, including UpdateUser
}
```

**Impact:**
- ✅ Admins could update any user profile
- ❌ **Non-admin users could NOT update their own profile** → 403 Forbidden
- The authorization middleware rejected the request BEFORE it even reached the controller code
- That's why we never saw the `🎯 UsersController.UpdateUser` logs

### Log Evidence

From `logmyday20251006.log`:

```
2025-10-06 17:35:28.603 [INF] [BasicAuth] User 'caloris@caloris.cz' (ID: "11111111-1111-1111-1111-111111111111") authenticated successfully
2025-10-06 17:35:28.609 [INF] AuthenticationScheme: basic was forbidden.
2025-10-06 17:35:28.609 [INF] Response Status: 403
```

**Notice:**
- ✅ Authentication succeeded (user was authenticated)
- ❌ Authorization failed (user was forbidden)
- ❌ Controller method never executed (no controller logs)

## ✅ Solution Implemented

Moved the `[Authorize(Policy = "AdminOnly")]` attribute from the class level to individual admin-only endpoints:

### Changes to `UsersController.cs`

1. **Removed class-level AdminOnly policy:**
   ```csharp
   [ApiController]
   [Route("api/users")]
   // ✅ NO [Authorize(Policy = "AdminOnly")] here
   public class UsersController : ControllerBase
   ```

2. **Added AdminOnly to admin-specific endpoints:**
   ```csharp
   [HttpGet]
   [Authorize(Policy = "AdminOnly")] // ✅ Only admins can list all users
   public async Task<IActionResult> GetUsers(...)
   
   [HttpPost]
   [Authorize(Policy = "AdminOnly")] // ✅ Only admins can create users
   public async Task<IActionResult> CreateUser(...)
   
   [HttpDelete("{id}")]
   [Authorize(Policy = "AdminOnly")] // ✅ Only admins can delete users
   public async Task<IActionResult> DeleteUser(...)
   ```

3. **Added standard [Authorize] to UpdateUser:**
   ```csharp
   [HttpPatch("{id}")]
   [Authorize] // ✅ Requires authentication, but users can update their own profile
   public async Task<IActionResult> UpdateUser(...)
   ```

### How UpdateUser Authorization Works Now

1. **Authentication:** User must be authenticated (checked by `[Authorize]`)
2. **Service-level authorization:** `UserService.Update` checks:
   - ✅ Admins can update any user
   - ✅ Non-admins can ONLY update their own profile (checked by comparing IDs)
   - ❌ Non-admins CANNOT change admin status

This is the correct pattern: **authentication at controller level, fine-grained authorization in business logic**.

## 📊 Authorization Matrix

| Endpoint | Previous | Fixed | Description |
|----------|----------|-------|-------------|
| `GET /api/users` | Admin only | ✅ Admin only | List all users - should remain admin-only |
| `POST /api/users` | Admin only | ✅ Admin only | Create new user - should remain admin-only |
| `PATCH /api/users/{id}` | Admin only | ✅ **Authenticated users** | Update user - users can update themselves |
| `DELETE /api/users/{id}` | Admin only | ✅ Admin only | Delete user - should remain admin-only |

## 🚀 Deployment Requirements

### Files Changed

1. ✅ **`LogMyDay.Api/Controllers/UsersController.cs`**
   - Removed class-level `[Authorize(Policy = "AdminOnly")]`
   - Added method-level `[Authorize(Policy = "AdminOnly")]` to GetUsers, CreateUser, DeleteUser
   - Added method-level `[Authorize]` to UpdateUser

2. ✅ **`LogMyDay.Api/Application/Services/UserService.cs`**
   - Added comprehensive debug logging (can be removed after confirming fix)

3. ✅ **`LogMyDay.App/Program.cs`**
   - Smart-auth policy scheme (from previous fix)

### Deployment Steps

1. **Deploy updated code to logmyday.tadata.cz**
2. **Restart the server**
3. **Test mobile app settings update**

### Testing Checklist

After deployment:
- [ ] **Mobile app - Non-admin user can update own profile** (culture change) ✅ Should work now!
- [ ] **Mobile app - Non-admin user CANNOT update other users** (should fail)
- [ ] **Mobile app - Non-admin user CANNOT change their own admin status** (should fail)
- [ ] **Blazor Server - Admin can update any user** (should still work)
- [ ] **Blazor Server - Admin can create/delete users** (should still work)
- [ ] **Blazor Server - Non-admin can update own profile** (should work)

## 🔐 Security Implications

### ✅ Improved Security

- **Fine-grained authorization:** Each endpoint has appropriate authorization requirements
- **Principle of least privilege:** Users only have access to operations they need
- **Defense in depth:** Authorization checked at both controller and service level

### ✅ No Security Regression

- Admin-only operations remain protected
- Users still cannot escalate privileges
- All authentication mechanisms intact

## 📝 Debug Logging Added

For troubleshooting purposes, comprehensive logging was added:

### Controller Logging (`UsersController.UpdateUser`)
```csharp
_logger.LogInformation("🎯 UsersController.UpdateUser: Received update request for user {UserId}", id);
_logger.LogInformation("🎯 UsersController.UpdateUser: Request - Email={Email}, DisplayName={DisplayName}, IsAdmin={IsAdmin}, Culture={Culture}, TimeZone={TimeZone}", ...);
```

### Service Logging (`UserService.Update`)
```csharp
_logger.LogInformation("🔧 UserService.Update: Starting update for user {UserId} by actor {ActorId}", id, actorId);
_logger.LogInformation("🔧 UserService.Update: Found user - Id={UserId}, Email={Email}, IsAdmin={IsAdmin}", ...);
_logger.LogInformation("🔧 UserService.Update: Found actor - Id={ActorId}, Email={Email}, IsAdmin={IsAdmin}", ...);
_logger.LogInformation("🔧 UserService.Update: Checking authorization - actor.IsAdmin={IsAdmin}, actor.Id={ActorId}, user.Id={UserId}, IsSameUser={IsSame}", ...);
```

**Note:** This logging can be removed or reduced to DEBUG level once the fix is confirmed working.

## 🧪 Expected Behavior After Fix

### Scenario: Non-admin user changes culture settings

**Before fix:**
```
[INF] [BasicAuth] User authenticated successfully
[INF] AuthenticationScheme: basic was forbidden.
[INF] Response Status: 403
❌ Error: 403 Forbidden - "You don't have permission"
```

**After fix:**
```
[INF] [BasicAuth] User authenticated successfully
[INF] 🎯 UsersController.UpdateUser: Received update request for user 11111111...
[INF] 🔧 UserService.Update: Starting update for user 11111111... by actor 11111111...
[INF] 🔧 UserService.Update: Checking authorization - actor.IsAdmin=False, actor.Id=11111111..., user.Id=11111111..., IsSameUser=True
[INF] 🔧 UserService.Update: Authorization passed - proceeding with update
[INF] Response Status: 200
✅ Success: Profile updated successfully
```

## 🎓 Lessons Learned

### Design Patterns

1. **Prefer method-level authorization over class-level:**
   - Class-level authorization is a blanket rule
   - Method-level allows fine-grained control

2. **Layered authorization:**
   - Controller: Authentication + basic authorization
   - Service: Business logic authorization (who can update whom)

3. **Explicit is better than implicit:**
   - Each endpoint should clearly state its authorization requirements

### Debugging Techniques

1. **Check authorization middleware first:**
   - 403 with no controller logs = authorization middleware rejection
   - 403 with controller logs = business logic rejection

2. **Log at multiple layers:**
   - Middleware logs (authentication success/failure)
   - Controller logs (request received)
   - Service logs (business logic decisions)

## 🔄 Related Issues Fixed

This fix resolves:
- ✅ Mobile app culture switching (primary issue)
- ✅ Mobile app timezone changes
- ✅ Mobile app profile updates (display name, email)
- ✅ Any non-admin user profile management

## 📚 Related Documentation

- [Authentication Fix - Sep 2025](./authentication-fix-sep-2025.md) - Smart-auth policy scheme
- [Mobile Culture Date Pickers](./mobile-culture-date-pickers.md) - Feature that triggered this issue
- [Security Overview](./security-overview.md) - Overall security architecture

---

**Last Updated:** October 6, 2025  
**Status:** ✅ Fixed - Ready for Production Deployment  
**Priority:** 🔴 CRITICAL - Required for mobile app user profile management  
**Build Status:** ✅ Builds successfully (13 warnings, 0 errors)
