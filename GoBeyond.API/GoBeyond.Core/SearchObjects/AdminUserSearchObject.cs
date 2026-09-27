using GoBeyond.Core.Enums;

namespace GoBeyond.Core.SearchObjects;

public class AdminUserSearchObject
{
    public string? Search { get; set; }
    public UserRole? Role { get; set; }
    public bool? IsActive { get; set; }
}
