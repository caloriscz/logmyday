using LogMyDay.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace LogMyDay.Api.Application.Interfaces;

public interface IAuthService
{
    Task SignInAsync(HttpContext httpContext, User user);
    Task SignOutAsync(HttpContext httpContext);
    Guid? GetUserId(ClaimsPrincipal principal);
}
