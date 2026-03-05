using GoBeyond.Core.Enums;

namespace GoBeyond.Core.DTOs.Auth;

public record AuthUserDto(int Id, string Name, string Email, UserRole Role);
