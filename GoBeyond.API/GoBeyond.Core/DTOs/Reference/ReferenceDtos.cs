using System.ComponentModel.DataAnnotations;

namespace GoBeyond.Core.DTOs.Reference;

public sealed class TrainingTypeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class TrainingTypeUpsertRequest
{
    private string _name = string.Empty;
    private string _description = string.Empty;

    [Required(ErrorMessage = "Naziv je obavezan.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Naziv mora imati 2–50 znakova.")]
    public string Name
    {
        get => _name;
        set => _name = value?.Trim() ?? string.Empty;
    }

    [Required(ErrorMessage = "Opis je obavezan.")]
    [StringLength(500, MinimumLength = 2, ErrorMessage = "Opis mora imati 2–500 znakova.")]
    public string Description
    {
        get => _description;
        set => _description = value?.Trim() ?? string.Empty;
    }
}

public sealed class FitnessGoalDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public sealed class FitnessGoalUpsertRequest
{
    private string _name = string.Empty;
    private string? _description;

    [Required(ErrorMessage = "Naziv je obavezan.")]
    [StringLength(60, MinimumLength = 2, ErrorMessage = "Naziv mora imati 2–60 znakova.")]
    public string Name
    {
        get => _name;
        set => _name = value?.Trim() ?? string.Empty;
    }

    [StringLength(300, ErrorMessage = "Opis može imati najviše 300 znakova.")]
    public string? Description
    {
        get => _description;
        set => _description = value?.Trim();
    }
}

public sealed class FitnessLevelDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
}

public sealed class FitnessLevelUpsertRequest
{
    private string _name = string.Empty;
    private string? _description;

    [Required(ErrorMessage = "Naziv je obavezan.")]
    [StringLength(40, MinimumLength = 2, ErrorMessage = "Naziv mora imati 2–40 znakova.")]
    public string Name
    {
        get => _name;
        set => _name = value?.Trim() ?? string.Empty;
    }

    [StringLength(300, ErrorMessage = "Opis može imati najviše 300 znakova.")]
    public string? Description
    {
        get => _description;
        set => _description = value?.Trim();
    }

    [Range(1, 100, ErrorMessage = "Redoslijed mora biti cijeli broj između 1 i 100.")]
    public int SortOrder { get; set; }
}

public sealed class GenderDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class GenderUpsertRequest
{
    private string _name = string.Empty;

    [Required(ErrorMessage = "Naziv je obavezan.")]
    [StringLength(30, MinimumLength = 2, ErrorMessage = "Naziv mora imati 2–30 znakova.")]
    public string Name
    {
        get => _name;
        set => _name = value?.Trim() ?? string.Empty;
    }
}
