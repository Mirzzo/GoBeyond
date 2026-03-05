namespace GoBeyond.Core.DTOs.Auth;

public record AuthResponseDto(string AccessToken, string RefreshToken, AuthUserDto User);
