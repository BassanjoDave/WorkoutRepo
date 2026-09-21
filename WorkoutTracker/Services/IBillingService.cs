#if ANDROID || IOS
using Plugin.InAppBilling;
#endif

namespace WorkoutTracker.Services;

public record ProductDetails(string ProductId, string LocalizedPrice, string Title);

/// <summary>TransactionIdentifier is what FinalizePurchaseAsync needs to
/// acknowledge/complete the purchase with the store — kept separate from
/// PurchaseToken because on iOS they're the same value (StoreKit2 has no
/// separate opaque token; see BillingService.ToOutcome) but on Android
/// they're genuinely different fields.</summary>
public record PurchaseOutcome(string ProductId, string PurchaseToken, string TransactionIdentifier, bool Success, string? ErrorMessage);

/// <summary>
/// Wraps Plugin.InAppBilling behind this app's own seam, matching the same
/// single-file, single-interface, #if-branched-class shape as
/// IWorkoutReminderService — Android/iOS get a real implementation, everything
/// else (Windows, MacCatalyst) is an unsupported stub, gated by IsSupported.
///
/// A purchase here is never trusted on its own: PurchaseAsync/RestorePurchasesAsync
/// only report what the store said happened. UpgradeViewModel sends the resulting
/// token to the server for real verification (see RemoteApiWorkoutRepository.
/// VerifyPurchaseAsync) before calling FinalizePurchaseAsync — Google Play
/// auto-refunds an unacknowledged purchase after 3 days, so a purchase that
/// fails server verification is deliberately left unfinalized (and therefore
/// retryable) rather than silently acknowledged and lost.
/// </summary>
public interface IBillingService
{
    bool IsSupported { get; }
    Task<bool> ConnectAsync();
    Task DisconnectAsync();
    Task<ProductDetails?> GetProductAsync(string productId);
    Task<PurchaseOutcome> PurchaseAsync(string productId);
    Task<IReadOnlyList<PurchaseOutcome>> RestorePurchasesAsync();
    Task<bool> FinalizePurchaseAsync(string transactionIdentifier);
}

public class BillingService : IBillingService
{
#if ANDROID || IOS
    public bool IsSupported => CrossInAppBilling.IsSupported;

    private static IInAppBilling Billing => CrossInAppBilling.Current;

    public async Task<bool> ConnectAsync()
    {
        try { return await Billing.ConnectAsync(); }
        catch { return false; }
    }

    public Task DisconnectAsync() => Billing.DisconnectAsync();

    public async Task<ProductDetails?> GetProductAsync(string productId)
    {
        try
        {
            var products = await Billing.GetProductInfoAsync(ItemType.Subscription, new[] { productId });
            var product = products?.FirstOrDefault();
            return product is null ? null : new ProductDetails(product.ProductId, product.LocalizedPrice, product.Name);
        }
        catch
        {
            return null;
        }
    }

    public async Task<PurchaseOutcome> PurchaseAsync(string productId)
    {
        try
        {
            var purchase = await Billing.PurchaseAsync(productId, ItemType.Subscription);
            if (purchase is null) return new PurchaseOutcome(productId, "", "", false, "Purchase did not complete.");
            return purchase.State is PurchaseState.Purchased or PurchaseState.Restored
                ? ToOutcome(purchase)
                : new PurchaseOutcome(productId, "", "", false, $"Purchase not completed (state: {purchase.State}).");
        }
        catch (InAppBillingPurchaseException ex) when (ex.PurchaseError == PurchaseError.UserCancelled)
        {
            // Not a real error to surface — the member just backed out of the sheet.
            return new PurchaseOutcome(productId, "", "", false, null);
        }
        catch (InAppBillingPurchaseException ex)
        {
            return new PurchaseOutcome(productId, "", "", false, ex.Message);
        }
    }

    public async Task<IReadOnlyList<PurchaseOutcome>> RestorePurchasesAsync()
    {
        try
        {
            var purchases = await Billing.GetPurchasesAsync(ItemType.Subscription);
            return purchases?.Select(ToOutcome).ToList() ?? new List<PurchaseOutcome>();
        }
        catch
        {
            return Array.Empty<PurchaseOutcome>();
        }
    }

    public async Task<bool> FinalizePurchaseAsync(string transactionIdentifier)
    {
        try
        {
            var results = await Billing.FinalizePurchaseAsync(new[] { transactionIdentifier });
            return results.Any(r => r.Success);
        }
        catch
        {
            return false;
        }
    }

    private static PurchaseOutcome ToOutcome(InAppBillingPurchase purchase)
    {
        // Android: PurchaseToken is the real opaque store token the server verifies.
        // iOS: StoreKit2 has no such token — Plugin.InAppBilling leaves PurchaseToken
        // empty there, so the original transaction id stands in for it, matching
        // AppleAppStoreVerifier's own "purchase token = original transaction id"
        // convention (see its doc comment).
        var token = !string.IsNullOrEmpty(purchase.PurchaseToken)
            ? purchase.PurchaseToken
            : purchase.OriginalTransactionIdentifier ?? purchase.TransactionIdentifier ?? "";
        return new PurchaseOutcome(purchase.ProductId, token, purchase.TransactionIdentifier ?? "", true, null);
    }
#else
    public bool IsSupported => false;
    public Task<bool> ConnectAsync() => Task.FromResult(false);
    public Task DisconnectAsync() => Task.CompletedTask;
    public Task<ProductDetails?> GetProductAsync(string productId) => Task.FromResult<ProductDetails?>(null);
    public Task<PurchaseOutcome> PurchaseAsync(string productId) =>
        Task.FromResult(new PurchaseOutcome(productId, "", "", false, "Purchases aren't supported on this platform."));
    public Task<IReadOnlyList<PurchaseOutcome>> RestorePurchasesAsync() =>
        Task.FromResult<IReadOnlyList<PurchaseOutcome>>(Array.Empty<PurchaseOutcome>());
    public Task<bool> FinalizePurchaseAsync(string transactionIdentifier) => Task.FromResult(false);
#endif
}
