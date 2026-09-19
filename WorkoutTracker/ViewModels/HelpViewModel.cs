using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// Static Help/FAQ/Support/About page, reached from Profile → "Help &amp; Support".
/// The support email is a placeholder — see the disclaimer on SupportEmail below,
/// same "flag it, don't hide it" convention as LegalViewModel's placeholder ToS/Privacy text.
/// </summary>
public partial class HelpViewModel : ObservableObject
{
    /// <summary>PLACEHOLDER — replace with a real, monitored support address before real users rely on this.</summary>
    public const string SupportEmail = "support@example.com";

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

    [RelayCommand]
    private async Task OpenTerms() => await Shell.Current.GoToAsync("legal?doc=terms");

    [RelayCommand]
    private async Task OpenPrivacy() => await Shell.Current.GoToAsync("legal?doc=privacy");
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
