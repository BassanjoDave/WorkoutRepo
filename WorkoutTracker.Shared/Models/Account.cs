namespace WorkoutTracker.Models;

public class Account
{
    public Guid Id { get; set; }
    public Guid PrimaryHolderMemberId { get; set; }
    public string DisplayName { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    // Billing seam. Every value below is a hardcoded free-tier constant until
    // a real billing layer exists — but every seat-granting operation must
    // already go through ISeatAvailabilityService rather than checking these
    // fields directly, so that layer is a drop-in later.
    public string PlanId { get; set; } = "free";
    // Matched to PremiumSeatCount's own hard cap (see IEntitlementService) so a
    // free household never has to remove a member before it can fully upgrade.
    public int SeatLimit { get; set; } = 12;
    // Paid tags currently active: "noads", "nutrition", "stacks", "measurements",
    // "fullaccess" (fullaccess satisfies any individual page check too). Any
    // non-empty array removes ads for the seats in PremiumMemberIds. Server-derived
    // from Purchases once real billing exists — see IEntitlementService, the seam
    // every gating check must go through instead of reading this array directly.
    public string[] Entitlements { get; set; } = Array.Empty<string>();
    public string BillingStatus { get; set; } = "active";

    // How many members Entitlements currently covers, derived server-side from
    // which seat-granting products (the base purchase seat + family_base +
    // up to 2 capacity blocks) are active. Capped at 12.
    public int PremiumSeatCount { get; set; } = 0;
    // Which specific members the primary holder has assigned the PremiumSeatCount
    // seats to (see ManageMembersViewModel). Client-writable — it's a preference,
    // not billing truth.
    public List<Guid> PremiumMemberIds { get; set; } = new();
    // One entry per store subscription this account has ever activated. The
    // source of truth Entitlements/PremiumSeatCount/PlanId are recomputed from
    // once server-side purchase verification exists (see the monetization plan) —
    // empty until then.
    public List<PurchaseRecord> Purchases { get; set; } = new();
}

public class PurchaseRecord
{
    public string ProductId { get; set; } = "";
    public string Platform { get; set; } = ""; // "android" / "ios"
    public string PurchaseToken { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public bool AutoRenewing { get; set; }
    public string Status { get; set; } = "active"; // "active" / "expired" / "grace" / "canceled"
}

/// <summary>Page-tag constants, so call sites never hand-type the raw strings. Lives
/// in Shared (not the client-only IEntitlementService.cs it used to live in) so the
/// server's purchase-verification code computes Account.Entitlements from the exact
/// same strings the client's gating checks compare against.</summary>
public static class PageEntitlements
{
    public const string Nutrition = "nutrition";
    public const string Stacks = "stacks";
    public const string Measurements = "measurements";
    public const string FullAccess = "fullaccess";
    public const string NoAds = "noads";
}
