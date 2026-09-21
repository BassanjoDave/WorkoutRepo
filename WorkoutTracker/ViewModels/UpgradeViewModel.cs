using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// Landing point for every paywall redirect (a gated page's LoadAsync, a locked
/// History tab, a locked Home card). Real billing phase 1: only "Full Access" is
/// wired to a real purchase (via IBillingService + server-side verification) —
/// the other tiers still show their existing pricing-preview copy until they're
/// added the same way. DevSettingsViewModel's manual entitlement toggles remain
/// for testing/support use even after this lands.
/// </summary>
public partial class UpgradeViewModel : ObservableObject, IQueryAttributable
{
    private readonly IBillingService _billing;
    private readonly RemoteApiWorkoutRepository _remoteRepo;
    private readonly IWorkoutRepository _activeRepo;
    private readonly IActiveSessionService _session;

    // Product IDs must exactly match what's created in Google Play Console/App
    // Store Connect — see the monetization plan's "Dave must do this first" list.
    public const string FullAccessMonthlyProductId = "full_access_monthly";
    public const string FullAccessYearlyProductId = "full_access_yearly";

    [ObservableProperty] public partial string ReasonLabel { get; set; } = "Unlock more of Rig Ritual";
    [ObservableProperty] public partial bool IsPurchasing { get; set; }
    [ObservableProperty] public partial string PurchaseStatus { get; set; } = "";

    public UpgradeViewModel(IBillingService billing, RemoteApiWorkoutRepository remoteRepo, IWorkoutRepository activeRepo, IActiveSessionService session)
    {
        _billing = billing;
        _remoteRepo = remoteRepo;
        _activeRepo = activeRepo;
        _session = session;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue("page", out var pageObj) || pageObj is not string page) return;
        ReasonLabel = page switch
        {
            PageEntitlements.Nutrition => "Nutrition is a paid feature",
            PageEntitlements.Stacks => "Stacks is a paid feature",
            PageEntitlements.Measurements => "Measurements is a paid feature",
            _ => ReasonLabel,
        };
    }

    /// <summary>Purchases one of the two Full Access products, then asks the server to
    /// verify the receipt with Google/Apple before trusting it — see
    /// RemoteApiWorkoutRepository.VerifyPurchaseAsync and EntitlementCalculator on the
    /// server. Only acknowledges/finalizes with the store once the server confirms
    /// (IBillingService.PurchaseAsync leaves an unacknowledged purchase retryable if
    /// verification fails, rather than silently losing the charge).</summary>
    [RelayCommand]
    private async Task PurchaseFullAccess(string productId)
    {
        var page = Shell.Current?.CurrentPage;
        if (page is null) return;

        if (!_billing.IsSupported)
        {
            await page.DisplayAlertAsync("Not available here", "Purchases aren't supported on this platform yet — try this on your phone.", "OK");
            return;
        }

        var account = _session.ActiveAccount;
        var member = _session.ActiveMember;
        if (account is null || member is null) return;

        IsPurchasing = true;
        PurchaseStatus = "";
        try
        {
            if (!await _billing.ConnectAsync())
            {
                PurchaseStatus = "Couldn't connect to the store. Try again in a moment.";
                return;
            }

            var outcome = await _billing.PurchaseAsync(productId);
            if (!outcome.Success)
            {
                PurchaseStatus = outcome.ErrorMessage ?? "Purchase didn't complete.";
                return;
            }

            await VerifyAndApplyAsync(account.Id, member.Id, outcome);
        }
        finally
        {
            IsPurchasing = false;
            await _billing.DisconnectAsync();
        }
    }

    /// <summary>Required by both app stores: re-links a purchase already made on
    /// another device (or before a reinstall) without charging again.</summary>
    [RelayCommand]
    private async Task RestorePurchases()
    {
        var account = _session.ActiveAccount;
        var member = _session.ActiveMember;
        if (account is null || member is null || !_billing.IsSupported) return;

        IsPurchasing = true;
        PurchaseStatus = "";
        try
        {
            await _billing.ConnectAsync();
            var restored = await _billing.RestorePurchasesAsync();
            if (restored.Count == 0)
            {
                PurchaseStatus = "No previous purchases found for this account.";
                return;
            }
            foreach (var outcome in restored)
            {
                await VerifyAndApplyAsync(account.Id, member.Id, outcome);
            }
        }
        finally
        {
            IsPurchasing = false;
            await _billing.DisconnectAsync();
        }
    }

    private async Task VerifyAndApplyAsync(Guid accountId, Guid memberId, PurchaseOutcome outcome)
    {
        if (!outcome.Success) return;
        var platform = DeviceInfo.Current.Platform == DevicePlatform.iOS ? "ios" : "android";

        VerifyPurchaseResult result;
        try
        {
            result = await _remoteRepo.VerifyPurchaseAsync(accountId, outcome.ProductId, platform, outcome.PurchaseToken);
        }
        catch (Exception ex)
        {
            PurchaseStatus = $"Purchase succeeded but couldn't be confirmed right now ({ex.Message}) — it'll apply next time you're online.";
            return;
        }

        if (!result.Success || result.Account is null)
        {
            PurchaseStatus = result.Error ?? "Couldn't verify this purchase.";
            return;
        }

        // VerifyPurchaseAsync writes straight to the server, bypassing the normal
        // sync-outbox (see its own doc comment) — so unlike every other account
        // write, nothing else refreshes this device's locally-cached AccountIndex.
        // Fold the server's recomputed billing fields into it here, exactly the
        // same load->mutate->save shape DevSettingsViewModel.SaveEntitlements uses.
        var accountIndex = await _activeRepo.GetAccountIndexAsync(accountId);
        if (accountIndex is null)
        {
            PurchaseStatus = "Purchase verified, but the app couldn't refresh locally — restart the app to see it unlocked.";
            return;
        }
        accountIndex.Account.Entitlements = result.Account.Entitlements;
        accountIndex.Account.PlanId = result.Account.PlanId;
        accountIndex.Account.BillingStatus = result.Account.BillingStatus;
        accountIndex.Account.PremiumSeatCount = result.Account.PremiumSeatCount;
        accountIndex.Account.PremiumMemberIds = result.Account.PremiumMemberIds;
        accountIndex.Account.Purchases = result.Account.Purchases;
        await _activeRepo.SaveAccountIndexAsync(accountIndex);

        // Only now — after the server has actually verified it — tell the store this
        // purchase is finalized. See IBillingService's own doc comment for why doing
        // this earlier (or not at all) risks either an auto-refund or an un-refundable
        // charge for something that never got recorded.
        if (!string.IsNullOrEmpty(outcome.TransactionIdentifier))
        {
            await _billing.FinalizePurchaseAsync(outcome.TransactionIdentifier);
        }

        // Same refresh DevSettingsViewModel.SaveEntitlements needs — without it, gated
        // pages keep reading the stale pre-purchase entitlements until app relaunch.
        await _session.SelectMemberAsync(accountId, memberId);
        PurchaseStatus = "Full Access unlocked!";
    }

    /// <summary>
    /// Deliberately navigates Home, not a relative ".." pop. This page is only ever
    /// reached via a redirect from a gated page whose own LoadAsync/OnAppearing
    /// re-checks entitlement every time it appears — popping back to that same page
    /// with nothing changed just re-triggers the exact same redirect immediately,
    /// which looks to the user like Back "just refreshing the Upgrade page" instead
    /// of actually going anywhere. Home is always a safe landing regardless of
    /// entitlement state. Wired as this page's Shell.BackButtonBehavior too (see
    /// UpgradePage.xaml), so this also covers the native toolbar/hardware back
    /// action, which otherwise bypasses this command and does Shell's own default
    /// relative pop.
    /// </summary>
    [RelayCommand]
    private async Task Back() => await Shell.Current.GoToAsync("//home");

    // Apple/Google both require a Terms of Use and Privacy Policy link reachable
    // from the actual subscription purchase screen, not just somewhere else in the
    // app — see UpgradePage.xaml's disclosure text. Same server-hosted pages as
    // HelpViewModel/ProfileGateViewModel's matching commands.
    [RelayCommand]
    private async Task OpenTerms() => await Microsoft.Maui.ApplicationModel.Launcher.Default.OpenAsync($"{RemoteApiConfig.BaseUrl}terms");

    [RelayCommand]
    private async Task OpenPrivacy() => await Microsoft.Maui.ApplicationModel.Launcher.Default.OpenAsync($"{RemoteApiConfig.BaseUrl}privacy");
}
