using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// Dev-only screen for exercising RemoteApiWorkoutRepository against the live
/// deployed API before any real UI flow uses it. The API key is entered here
/// and kept only in SecureStorage — never written to a file in this project.
/// Delete this screen once Google Sign-In replaces the shared-key gate.
///
/// Also carries Stage 1 of the monetization build: manual entitlement toggles
/// standing in for real billing (see IEntitlementService/IBillingService), so
/// the paywall gating across Nutrition/Stacks/Measurements/History can be proven
/// end to end before any store integration exists. Remove these once
/// IBillingService is wired up for real.
/// </summary>
public partial class DevSettingsViewModel : ObservableObject
{
    private readonly RemoteApiWorkoutRepository _remoteRepo;
    private readonly IWorkoutRepository _activeRepo;
    private readonly ISyncStatusService _syncStatus;
    private readonly IActiveSessionService _session;
    private readonly IMemberAuthGateService _authGate;

    /// <summary>See the matching field on ManageMembersViewModel for why this exists —
    /// avoids re-prompting on every back-navigation onto an already-open instance.</summary>
    private bool _verifiedThisOpen;

    [ObservableProperty] public partial string ApiKeyInput { get; set; } = "";
    [ObservableProperty] public partial string StatusMessage { get; set; } = "";

    [ObservableProperty] public partial bool EntitlementNoAds { get; set; }
    [ObservableProperty] public partial bool EntitlementNutrition { get; set; }
    [ObservableProperty] public partial bool EntitlementStacks { get; set; }
    [ObservableProperty] public partial bool EntitlementMeasurements { get; set; }
    [ObservableProperty] public partial bool EntitlementFullAccess { get; set; }
    [ObservableProperty] public partial int PremiumSeatCountInput { get; set; }
    [ObservableProperty] public partial string EntitlementStatus { get; set; } = "";

    public DevSettingsViewModel(RemoteApiWorkoutRepository remoteRepo, IWorkoutRepository activeRepo, ISyncStatusService syncStatus, IActiveSessionService session, IMemberAuthGateService authGate)
    {
        _remoteRepo = remoteRepo;
        _activeRepo = activeRepo;
        _syncStatus = syncStatus;
        _session = session;
        _authGate = authGate;
    }

    public async Task LoadAsync()
    {
        ApiKeyInput = await RemoteApiConfig.GetApiKeyAsync() ?? "";

        var account = _session.ActiveAccount;
        if (account is null) return; // reachable pre-sign-in too (API key setup) — no holder to gate against yet

        // Settings lockdown — see the matching comment on ManageMembersViewModel.LoadAsync.
        if (!_verifiedThisOpen)
        {
            var accountIndex = await _activeRepo.GetAccountIndexAsync(account.Id);
            var holder = accountIndex?.Members.FirstOrDefault(m => m.Id == account.PrimaryHolderMemberId);
            if (accountIndex is not null && holder is not null
                && !await _authGate.VerifyOrEstablishAsync(holder, accountIndex, _activeRepo, "Open Developer Settings"))
            {
                await Shell.Current.GoToAsync("..");
                return;
            }
            _verifiedThisOpen = true;
        }

        EntitlementNoAds = account.Entitlements.Contains(PageEntitlements.NoAds);
        EntitlementNutrition = account.Entitlements.Contains(PageEntitlements.Nutrition);
        EntitlementStacks = account.Entitlements.Contains(PageEntitlements.Stacks);
        EntitlementMeasurements = account.Entitlements.Contains(PageEntitlements.Measurements);
        EntitlementFullAccess = account.Entitlements.Contains(PageEntitlements.FullAccess);
        PremiumSeatCountInput = account.PremiumSeatCount;
    }

    /// <summary>Writes the toggles above straight onto the active account's billing
    /// fields and makes sure the current member holds a premium seat, so the change
    /// is visible immediately without a separate trip to Manage Members. This is the
    /// stand-in for a real purchase completing — see IBillingService in the plan.
    ///
    /// Explicitly re-selects the member afterward to refresh IActiveSessionService's
    /// in-memory Account copy — without this, every gated page keeps reading the
    /// stale pre-save entitlements for the rest of the app session (confirmed by
    /// testing: the save took effect immediately in local storage, but Nutrition
    /// stayed locked until a full app relaunch). Real billing will need the same
    /// refresh-after-purchase step, per the plan's "sync-latency fix" note.</summary>
    private static readonly string DebugLogPath = Path.Combine(FileSystem.AppDataDirectory, "saveent-debug.log");

    private static void DebugLog(string message)
    {
        try { File.AppendAllText(DebugLogPath, $"{DateTime.UtcNow:O} {message}\n"); } catch { /* diagnostic only */ }
    }

    [RelayCommand]
    private async Task SaveEntitlements()
    {
        DebugLog("command invoked");
        var account = _session.ActiveAccount;
        var member = _session.ActiveMember;
        if (account is null || member is null) { EntitlementStatus = "No active account/member."; DebugLog("no account/member"); return; }

        DebugLog("step 1: loading account...");
        EntitlementStatus = "1: loading account...";
        try
        {
            var accountIndex = await _activeRepo.GetAccountIndexAsync(account.Id);
            DebugLog("step 1 complete, accountIndex null=" + (accountIndex is null));
            if (accountIndex is null) { EntitlementStatus = "Couldn't load account."; return; }
            EntitlementStatus = "2: writing tags...";
            DebugLog("step 2: writing tags...");

            var tags = new List<string>();
            if (EntitlementNoAds) tags.Add(PageEntitlements.NoAds);
            if (EntitlementNutrition) tags.Add(PageEntitlements.Nutrition);
            if (EntitlementStacks) tags.Add(PageEntitlements.Stacks);
            if (EntitlementMeasurements) tags.Add(PageEntitlements.Measurements);
            if (EntitlementFullAccess) tags.Add(PageEntitlements.FullAccess);
            accountIndex.Account.Entitlements = tags.ToArray();
            accountIndex.Account.PremiumSeatCount = Math.Max(0, PremiumSeatCountInput);

            if (accountIndex.Account.PremiumSeatCount > 0 && !accountIndex.Account.PremiumMemberIds.Contains(member.Id))
            {
                accountIndex.Account.PremiumMemberIds.Add(member.Id);
            }

            EntitlementStatus = "3: saving to disk...";
            DebugLog("step 3: saving to disk, tags=[" + string.Join(",", tags) + "]");
            await _activeRepo.SaveAccountIndexAsync(accountIndex);
            DebugLog("step 3 complete");
            EntitlementStatus = "4: saved. refreshing session...";
            DebugLog("step 4: calling SelectMemberAsync...");
            var refreshed = await _session.SelectMemberAsync(account.Id, member.Id);
            DebugLog("step 4 complete, refreshed null=" + (refreshed is null));
            EntitlementStatus = "5: session refreshed. finishing...";
            var liveTags = _session.ActiveAccount?.Entitlements ?? Array.Empty<string>();
            DebugLog("step 5: liveTags=[" + string.Join(",", liveTags) + "]");
            EntitlementStatus = refreshed is null
                ? "Saved, but SelectMemberAsync returned null (member lookup failed on refresh)."
                : $"Saved. Live session now shows: [{string.Join(",", liveTags)}]";
            DebugLog("done, EntitlementStatus=" + EntitlementStatus);
        }
        catch (Exception ex)
        {
            DebugLog("EXCEPTION: " + ex);
            EntitlementStatus = $"ERROR: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task Back() => await Shell.Current.GoToAsync("..");

    [RelayCommand]
    private async Task SaveKey()
    {
        await RemoteApiConfig.SetApiKeyAsync(ApiKeyInput.Trim());
        StatusMessage = "Key saved to SecureStorage.";
    }

    [RelayCommand]
    private async Task TestConnection()
    {
        StatusMessage = "Testing...";
        try
        {
            var library = await _remoteRepo.GetManufacturerLibraryAsync();
            StatusMessage = $"Connected. Manufacturer library: {library.Exercises.Count} exercises, {library.Routines.Count} routines.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ForceSync()
    {
        StatusMessage = "Draining outbox...";
        if (_activeRepo is SyncingWorkoutRepository syncing) await syncing.DrainAsync();
        StatusMessage = $"Sync state: {_syncStatus.State}, {_syncStatus.PendingCount} pending."
            + (_syncStatus.LastError is string err ? $"\nLast error: {err}" : "");
    }
}
