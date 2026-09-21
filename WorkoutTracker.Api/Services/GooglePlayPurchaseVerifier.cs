using Google.Apis.AndroidPublisher.v3;
using Google.Apis.AndroidPublisher.v3.Data;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;

namespace WorkoutTracker.Api.Services;

public record GooglePlayVerificationResult(bool Success, DateTimeOffset ExpiresAt, bool AutoRenewing, string Status, string? Error);

/// <summary>
/// Verifies an Android purchase/subscription token against the Google Play
/// Developer API — never trusts the token or its claimed state from the client
/// alone. Uses the modern Subscriptionsv2.Get (package name + token only, no
/// separate subscription/product ID needed), the API Google's own RTDN docs
/// recommend pairing with real-time developer notifications. Needs a Google
/// Cloud service account with the Android Publisher API enabled and granted
/// access in Play Console → Users and permissions (Dave's manual setup — see
/// the monetization plan).
/// </summary>
public class GooglePlayPurchaseVerifier
{
    private readonly AndroidPublisherService _service;
    private readonly string _packageName;

    public GooglePlayPurchaseVerifier(string serviceAccountJson, string packageName)
    {
        _packageName = packageName;
        var credential = CredentialFactory.FromJson<ServiceAccountCredential>(serviceAccountJson)
            .ToGoogleCredential()
            .CreateScoped(AndroidPublisherService.Scope.Androidpublisher);
        _service = new AndroidPublisherService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "WorkoutTracker API",
        });
    }

    public async Task<GooglePlayVerificationResult> VerifyAsync(string purchaseToken)
    {
        SubscriptionPurchaseV2 subscription;
        try
        {
            subscription = await _service.Purchases.Subscriptionsv2.Get(_packageName, purchaseToken).ExecuteAsync();
        }
        catch (Exception ex)
        {
            return new GooglePlayVerificationResult(false, default, false, "expired", ex.Message);
        }

        // SUBSCRIPTION_STATE_ACTIVE / _IN_GRACE_PERIOD / _CANCELED / _ON_HOLD / _EXPIRED / _PAUSED.
        var status = subscription.SubscriptionState switch
        {
            "SUBSCRIPTION_STATE_ACTIVE" => "active",
            "SUBSCRIPTION_STATE_IN_GRACE_PERIOD" => "grace",
            "SUBSCRIPTION_STATE_CANCELED" => "canceled",
            _ => "expired",
        };

        // Line items share one expiry for a single-product subscription like ours —
        // take the latest if Google ever returns more than one.
        var expiresAt = subscription.LineItems?
            .Select(li => li.ExpiryTimeDateTimeOffset)
            .Where(d => d is not null)
            .Select(d => d!.Value)
            .DefaultIfEmpty(DateTimeOffset.UtcNow)
            .Max() ?? DateTimeOffset.UtcNow;

        var autoRenewing = subscription.LineItems?.Any(li => li.AutoRenewingPlan?.AutoRenewEnabled == true) ?? false;

        return new GooglePlayVerificationResult(true, expiresAt, autoRenewing, status, null);
    }
}
