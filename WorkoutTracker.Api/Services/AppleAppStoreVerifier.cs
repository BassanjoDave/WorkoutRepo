using Mimo.AppStoreServerLibrary;
using Mimo.AppStoreServerLibrary.Models;

namespace WorkoutTracker.Api.Services;

public record AppleVerificationResult(bool Success, DateTimeOffset ExpiresAt, bool AutoRenewing, string Status, string? Error);

/// <summary>
/// Verifies an iOS purchase/subscription against Apple's App Store Server API —
/// never trusts a client-reported transaction id's status alone. The "purchase
/// token" IBillingService/UpgradeViewModel pass around for iOS is really an
/// original transaction identifier (StoreKit2-era), not an opaque token like
/// Android's — a subscription keeps the same original transaction id across every
/// renewal, which is exactly what GetAllSubscriptionStatuses below expects.
///
/// Needs an App Store Connect API key (.p8 private key + Key ID + Issuer ID) from
/// Secret Manager (Dave's manual setup — see the monetization plan), and Apple's
/// own public Root CA certificate to verify the JWS signatures on everything
/// Apple returns. That certificate is a public trust anchor, not a secret — it's
/// checked into this project under AppleCertificates/AppleRootCA-G3.cer, embedded
/// as a resource.
/// </summary>
public class AppleAppStoreVerifier
{
    private readonly AppStoreServerApiClient _apiClient;
    private readonly SignedDataVerifier _signedDataVerifier;

    public AppleAppStoreVerifier(string privateKeyP8, string keyId, string issuerId, string bundleId, bool sandbox)
    {
        var environment = sandbox ? AppStoreEnvironment.Sandbox : AppStoreEnvironment.Production;
        _apiClient = new AppStoreServerApiClient(privateKeyP8, keyId, issuerId, bundleId, environment);

        using var certStream = typeof(AppleAppStoreVerifier).Assembly.GetManifestResourceStream(
            "WorkoutTracker.Api.AppleCertificates.AppleRootCA-G3.cer")
            ?? throw new InvalidOperationException("Embedded Apple root certificate not found.");
        using var buffer = new MemoryStream();
        certStream.CopyTo(buffer);
        _signedDataVerifier = new SignedDataVerifier(new[] { buffer.ToArray() }, enableOnlineChecks: true, environment, bundleId);
    }

    /// <summary>originalTransactionId: see this class's own doc comment for why
    /// that's what iOS's "purchase token" really is.</summary>
    public async Task<AppleVerificationResult> VerifyAsync(string originalTransactionId)
    {
        SubscriptionStatusResponse response;
        try
        {
            response = await _apiClient.GetAllSubscriptionStatuses(originalTransactionId);
        }
        catch (Exception ex)
        {
            return new AppleVerificationResult(false, default, false, "expired", ex.Message);
        }

        // Full Access is a single subscription group in this phase, so there's
        // exactly one relevant last transaction across every group Apple returns.
        var last = response.Data?.SelectMany(g => g.LastTransactions).FirstOrDefault();
        if (last?.SignedTransactionInfo is null) return new AppleVerificationResult(false, default, false, "expired", "No subscription found for this transaction.");

        var status = last.Status switch
        {
            TransactionsItemSubscriptionStatus.Active => "active",
            TransactionsItemSubscriptionStatus.BillingGracePeriod => "grace",
            TransactionsItemSubscriptionStatus.BillingRetryPeriod => "grace",
            _ => "expired", // Expired, Revoked — a refund/chargeback loses access immediately.
        };

        var transaction = await _signedDataVerifier.VerifyAndDecodeTransaction(last.SignedTransactionInfo);
        var expiresAt = DateTimeOffset.FromUnixTimeMilliseconds(transaction.ExpiresDate);

        var autoRenewing = false;
        if (!string.IsNullOrEmpty(last.SignedRenewalInfo))
        {
            var renewal = await _signedDataVerifier.VerifyAndDecodeRenewalInfo(last.SignedRenewalInfo);
            autoRenewing = renewal.AutoRenewStatus == 1;
        }

        return new AppleVerificationResult(true, expiresAt, autoRenewing, status, null);
    }

    public record DecodedNotification(string PurchaseToken, string NotificationType, string? SubType);

    /// <summary>Verifies and decodes a webhook payload's JWS signature — see
    /// Program.cs's POST /webhooks/apple. The returned PurchaseToken is the
    /// original transaction id, matching VerifyAsync's own parameter, so
    /// ApplyPurchaseUpdateAsync can treat it the same as every other platform's
    /// token when resolving the purchase-tokens/ lookup document.</summary>
    public async Task<(bool Success, string? Error, DecodedNotification? Notification)> DecodeNotificationAsync(string signedPayload)
    {
        ResponseBodyV2DecodedPayload decoded;
        try
        {
            decoded = await _signedDataVerifier.VerifyAndDecodeNotification(signedPayload);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, null);
        }

        var signedTransactionInfo = decoded.Data?.SignedTransactionInfo;
        if (signedTransactionInfo is null) return (false, "Notification carried no transaction info.", null);

        var transaction = await _signedDataVerifier.VerifyAndDecodeTransaction(signedTransactionInfo);
        return (true, null, new DecodedNotification(transaction.OriginalTransactionId, decoded.NotificationType, decoded.Subtype));
    }
}
