using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// Static Help/FAQ/Support/About page, reached from Profile → "Help &amp; Support".
/// </summary>
public partial class HelpViewModel : ObservableObject
{
    /// <summary>The real address, but not yet live/monitored until rigritual.com's
    /// email forwarding is set up (Dave's manual step — see the punch list). Keep
    /// this in sync with Program.cs's contact links in PrivacyPolicyHtml/
    /// TermsOfServiceHtml, which use the same address.</summary>
    public const string SupportEmail = "support@rigritual.com";

    [ObservableProperty] public partial string AppVersionLabel { get; set; } = "";
    public ObservableCollection<FaqItemViewModel> FaqItems { get; }

    public HelpViewModel()
    {
        AppVersionLabel = $"Rig Ritual {AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})";
        FaqItems = new ObservableCollection<FaqItemViewModel>
        {
            new("How do I switch between family members?",
                "Tap your avatar in the app header (or Profile → Switch) to open the member picker."),
            new("How do I add a custom exercise?",
                "In the Ritual Builder, open \"Add Exercise,\" then pick \"+ Add custom exercise…\" from the Exercise dropdown."),
            new("How do I set up a reminder for a workout?",
                "Open the routine in its builder, tap \"Schedule & Reminder,\" and turn on \"Scheduled\" — reminders also need to be enabled once in Profile → Workout Reminders."),
            new("How is my data synced across devices?",
                "Your account syncs automatically whenever you have a connection — the sync status shows at the top of Home."),
            new("How do I upgrade to Full Access?",
                "Any locked page (Nutrition, Stacks, Measurements) shows an \"Upgrade\" banner — tap it to see the available plans."),
            new("How do I delete my account?",
                "Profile → Delete Account, at the bottom of the page (account holders only). This permanently deletes every member, workout, meal, measurement, and supplement log — it cannot be undone."),
        };
    }

    [RelayCommand]
    private async Task ContactSupport()
    {
        try
        {
            await Email.Default.ComposeAsync(new EmailMessage
            {
                Subject = "Rig Ritual support",
                Body = $"\n\n---\n{AppVersionLabel}\n{DeviceInfo.Current.Platform} {DeviceInfo.Current.VersionString}",
                To = new List<string> { SupportEmail },
            });
        }
        catch (FeatureNotSupportedException)
        {
            if (Shell.Current?.CurrentPage is Page page)
                await page.DisplayAlertAsync("No email app found", $"Reach support at {SupportEmail}", "OK");
        }
    }

    // Opens the same server-hosted pages Program.cs serves at /terms and /privacy —
    // the single real, canonical copy of each (see Program.cs's TermsOfServiceHtml/
    // PrivacyPolicyHtml), rather than a second in-app copy that could drift out of
    // sync with it. Also means Dave can fix a typo or update a date without an app
    // store release.
    [RelayCommand]
    private async Task OpenTerms() => await Microsoft.Maui.ApplicationModel.Launcher.Default.OpenAsync($"{RemoteApiConfig.BaseUrl}terms");

    [RelayCommand]
    private async Task OpenPrivacy() => await Microsoft.Maui.ApplicationModel.Launcher.Default.OpenAsync($"{RemoteApiConfig.BaseUrl}privacy");
}

public class FaqItemViewModel
{
    public string Question { get; }
    public string Answer { get; }

    public FaqItemViewModel(string question, string answer)
    {
        Question = question;
        Answer = answer;
    }
}
