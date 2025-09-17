namespace LogMyDay.Shared.DTOs;

public record RegisterFirstDto(string Email, string Password, string? DisplayName);

public record LoginDto(string Email, string Password);

public record CreateUserDto(string Email, string Password, string? DisplayName, bool IsAdmin);

public record UpdateUserDto(string? Email, string? DisplayName, bool? IsAdmin);

public record ChangePasswordDto(string CurrentPassword, string NewPassword);

public record AdminResetPasswordDto(string NewPassword);

public record ForgotDto(string Email);

public record ForgotConfirmDto(string Token, string NewPassword);

public record UserDto(Guid Id, string Email, string? DisplayName, bool IsAdmin, DateTime CreatedUtc, DateTime UpdatedUtc);

public record CurrentUserDto(Guid Id, string Email, string? DisplayName, bool IsAdmin);

public record ForgotResponseDto(string Message);

public record CsrfTokenDto(string Token);
