namespace GoBeyond.Core.DTOs.Auth;

public record RegisterClientRequestDto(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    decimal Weight,
    decimal Height,
    int Age,
    string FitnessLevel
);
