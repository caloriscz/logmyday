namespace LogMyDay.Shared.DTOs;

public record RegisterFirstDto(string Email, string Password, string? DisplayName);

public record LoginDto(string Email, string Password);

public record CreateUserDto(string Email, string Password, string? DisplayName, bool IsAdmin, string Culture, string TimeZone);

public record UpdateUserDto(string? Email, string? DisplayName, bool? IsAdmin, string? Culture, string? TimeZone, string? ActivityDisplayType = null, string? ActivitySortOrder = null, string? ActivityPeriodSort = null);

public record ChangePasswordDto(string CurrentPassword, string NewPassword);

public record AdminResetPasswordDto(string NewPassword);

public record ForgotDto(string Email);

public record ForgotConfirmDto(string Token, string NewPassword);

public record UserDto(Guid Id, string Email, string? DisplayName, bool IsAdmin, DateTime CreatedUtc, DateTime UpdatedUtc, string Culture, string TimeZone, string ActivityDisplayType = "daily", string ActivitySortOrder = "desc", string ActivityPeriodSort = "desc");

public record CurrentUserDto(Guid Id, string Email, string? DisplayName, bool IsAdmin, string Culture, string TimeZone, string ActivityDisplayType = "daily", string ActivitySortOrder = "desc", string ActivityPeriodSort = "desc");

public record ForgotResponseDto(string Message);

public record CsrfTokenDto(string Token);
