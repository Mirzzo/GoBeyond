namespace GoBeyond.Core.DTOs.Common;

/// <summary>Rezultat liste šifarnika (generički BaseCRUD obrazac).</summary>
public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
}

public sealed record MessageResponse(string Message);

public sealed record CountResponse(int Count);

public sealed record RoleDto(string Value, string Name);
