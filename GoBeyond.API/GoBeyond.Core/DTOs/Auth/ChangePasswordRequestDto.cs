namespace GoBeyond.Core.DTOs.Auth;

public record ChangePasswordRequestDto(string CurrentPassword, string NewPassword);
