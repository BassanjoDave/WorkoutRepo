using WorkoutTracker.Models;

namespace WorkoutTracker.Services;

/// <summary>
/// The seam the billing layer takes over later — mirrors ISeatAvailabilityService's
/// shape exactly. Every page-gate or ads-display check must call through here rather
/// than reading Account.Entitlements/PremiumMemberIds directly, so real purchase
/// verification can replace this implementation without touching call sites.
/// "fullaccess" in Entitlements always satisfies any individual page tag AND removes
/// ads. A bare page tag (e.g. "nutrition") on its own does NOT remove ads — a la carte
/// pages are only ever sold alongside "noads" (or bundled into "fullaccess"), so ad
/// removal is judged strictly by "noads"/"fullaccess" being present, never inferred
/// from "the account has some entitlement or other."
/// </summary>
public interface IEntitlementService
{
    bool HasPageAccess(Account account, Guid memberId, string pageTag);
    bool AdsRemovedFor(Account account, Guid memberId);
}

public class EntitlementService : IEntitlementService
{
    public bool HasPageAccess(Account account, Guid memberId, string pageTag)
    {
        if (!IsPremiumSeat(account, memberId)) return false;
        return account.Entitlements.Contains(pageTag) || account.Entitlements.Contains(PageEntitlements.FullAccess);
    }

    public bool AdsRemovedFor(Account account, Guid memberId) =>
        IsPremiumSeat(account, memberId) &&
        (account.Entitlements.Contains(PageEntitlements.NoAds) || account.Entitlements.Contains(PageEntitlements.FullAccess));

    private static bool IsPremiumSeat(Account account, Guid memberId) =>
        account.PremiumMemberIds.Contains(memberId);
}

/// <summary>Page-tag constants, so call sites never hand-type the raw strings.</summary>
public static class PageEntitlements
{
    public const string Nutrition = "nutrition";
    public const string Stacks = "stacks";
    public const string Measurements = "measurements";
    public const string FullAccess = "fullaccess";
    public const string NoAds = "noads";
}
