using LogMyDay.Shared.DTOs;
using Refit;

namespace LogMyDay.Shared.Interfaces;

public interface IAuthApi
{
    [Post("/api/auth/register-first")]
    Task RegisterFirstAdminAsync([Body] RegisterFirstDto request, CancellationToken cancellationToken = default);

    [Post("/api/auth/login")]
    Task LoginAsync([Body] LoginDto request, CancellationToken cancellationToken = default);

    [Post("/api/auth/logout")]
    Task LogoutAsync(CancellationToken cancellationToken = default);

    [Get("/api/me")]
    Task<CurrentUserDto> GetCurrentUserAsync(CancellationToken cancellationToken = default);

    [Get("/api/csrf")]
    Task<CsrfTokenDto> GetCsrfTokenAsync(CancellationToken cancellationToken = default);
}

public interface IUsersApi
{
    [Get("/api/users")]
    Task<List<UserDto>> GetUsersAsync(CancellationToken cancellationToken = default);

    [Post("/api/users")]
    Task<UserDto> CreateUserAsync([Body] CreateUserDto request, CancellationToken cancellationToken = default);

    [Patch("/api/users/{id}")]
    Task<UserDto> UpdateUserAsync(Guid id, [Body] UpdateUserDto request, CancellationToken cancellationToken = default);

    [Delete("/api/users/{id}")]
    Task DeleteUserAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IAccountApi
{
    [Post("/api/account/password/change")]
    Task ChangePasswordAsync([Body] ChangePasswordDto request, CancellationToken cancellationToken = default);

    [Post("/api/account/password/reset/{id}")]
    Task AdminResetPasswordAsync(Guid id, [Body] AdminResetPasswordDto request, CancellationToken cancellationToken = default);

    [Post("/api/account/password/forgot")]
    Task<ForgotResponseDto> ForgotPasswordAsync([Body] ForgotDto request, CancellationToken cancellationToken = default);

    [Post("/api/account/password/forgot/confirm")]
    Task ConfirmForgotPasswordAsync([Body] ForgotConfirmDto request, CancellationToken cancellationToken = default);
}
