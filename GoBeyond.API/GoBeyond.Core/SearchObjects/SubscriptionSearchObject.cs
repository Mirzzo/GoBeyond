using GoBeyond.Core.Enums;

namespace GoBeyond.Core.SearchObjects;

public class SubscriptionSearchObject : BaseSearchObject
{
    public int? MentorProfileId { get; set; }
    public int? ClientProfileId { get; set; }
    public SubscriptionStatus? Status { get; set; }
}
