using System.Security.Cryptography;
using System.Text;
using WorkoutTracker.Models;

namespace WorkoutTracker.Api.Services;

/// <summary>
/// Small lookup document mapping a raw store purchase token to the account it
/// belongs to — a renewal/cancellation webhook from Google or Apple only ever
/// carries a purchase token, never an accountId, so both webhook handlers need
/// this to find whose Account to update. Written once, at first successful
/// verification of that token (see Program.cs's /purchases/verify). Uses the
/// same IDriveDocumentStore flat-JSON-document pattern as every other
/// server-side lookup (see identities/directory.json), just keyed per-token
/// instead of one shared growing file, since purchase tokens are already
/// unique, opaque, and numerous.
/// </summary>
public class PurchaseTokenLookup
{
    public Guid AccountId { get; set; }
    public string ProductId { get; set; } = "";
    public string Platform { get; set; } = "";

    /// <summary>Purchase tokens can contain characters Drive's filename query doesn't
    /// love (and are long enough to be awkward filenames) — hash to a fixed-length,
    /// filesystem-safe name instead of using the raw token.</summary>
    public static string DocumentPath(string purchaseToken) =>
        $"purchase-tokens/{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(purchaseToken)))}.json";
}

/// <summary>
/// Pure function: derives Account.Entitlements/PremiumSeatCount/PlanId/BillingStatus
/// from whatever's currently in Account.Purchases. This is the ONE place "which
/// product IDs grant which entitlement" is decided — /purchases/verify and both
/// renewal webhooks all just update Purchases and then call this, so adding the
/// remaining pricing tiers (No Ads, per-page unlocks, Family plan seat blocks) later
/// is a change to the tables below, not to any endpoint.
/// </summary>
public static class EntitlementCalculator
{
    // Phase 1: Full Access only.
    private static readonly HashSet<string> FullAccessProductIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "full_access_monthly", "full_access_yearly",
    };

    public static void Recompute(Account account)
    {
        var now = DateTimeOffset.UtcNow;
        // "grace" = payment failed but the store is still retrying; "canceled" =
        // auto-renew turned off but the current paid period hasn't ended yet —
        // both keep access until ExpiresAt, same as every real subscription
        // product (turning off auto-renew doesn't refund the time already paid
        // for). Only "expired" (including a revoked/refunded purchase, which the
        // verifiers map straight to "expired" rather than "canceled" — see
        // AppleAppStoreVerifier.VerifyAsync) drops access immediately.
        var active = account.Purchases
            .Where(p => p.Status is "active" or "grace" or "canceled" && p.ExpiresAt > now)
            .ToList();

        var entitlements = new List<string>();
        if (active.Any(p => FullAccessProductIds.Contains(p.ProductId)))
        {
            entitlements.Add(PageEntitlements.FullAccess);
        }

        account.Entitlements = entitlements.ToArray();
        account.PlanId = entitlements.Contains(PageEntitlements.FullAccess) ? "full_access" : "free";
        account.PremiumSeatCount = entitlements.Count > 0 ? 1 : 0;
        account.BillingStatus = active.Count > 0
            ? (active.Any(p => p.Status == "grace") ? "grace" : "active")
            : "active"; // no active purchase at all just means "free," not a billing problem

        // PremiumMemberIds is client-writable (a preference, not billing truth — see
        // Account.cs's own comment) but must never exceed what's actually been paid
        // for. Trim down to the new seat count if it shrank, keeping the account
        // holder's own seat first if they'd already claimed one.
        if (account.PremiumMemberIds.Count > account.PremiumSeatCount)
        {
            var trimmed = account.PremiumMemberIds
                .OrderByDescending(id => id == account.PrimaryHolderMemberId)
                .Take(account.PremiumSeatCount)
                .ToList();
            account.PremiumMemberIds.Clear();
            account.PremiumMemberIds.AddRange(trimmed);
        }
    }
}
