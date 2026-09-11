using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Services;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// Landing point for every paywall redirect (a gated page's LoadAsync, a locked
/// History tab, a locked Home card). Stage 1 of the monetization build: shows the
/// locked pricing catalog and which page sent the member here, but purchasing isn't
/// wired up yet — IBillingService lands in a later pass and replaces the "Coming
/// soon" buttons below with real store purchases. Until then, entitlements are set
/// by hand in Developer Settings to prove the gating logic end to end.
/// </summary>
public partial class UpgradeViewModel : ObservableObject, IQueryAttributable
{
    [ObservableProperty] public partial string ReasonLabel { get; set; } = "Unlock more of Rig Ritual";

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
}
