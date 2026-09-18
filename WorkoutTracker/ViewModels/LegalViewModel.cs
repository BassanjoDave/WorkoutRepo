using CommunityToolkit.Mvvm.ComponentModel;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// Static Terms of Service / Privacy Policy text, reached from the sign-in
/// screen's "Terms" and "Privacy Policy" links. The text itself is placeholder
/// boilerplate, not reviewed legal copy — see the disclaimer baked into both
/// bodies below. Replace before shipping to real users.
/// </summary>
public partial class LegalViewModel : ObservableObject, IQueryAttributable
{
    [ObservableProperty] public partial string PageTitle { get; set; } = "";
    [ObservableProperty] public partial string Body { get; set; } = "";

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var doc = query.TryGetValue("doc", out var d) ? (string)d : "terms";
        if (doc == "privacy")
        {
            PageTitle = "Privacy Policy";
            Body = PrivacyPlaceholder;
        }
        else
        {
            PageTitle = "Terms of Service";
            Body = TermsPlaceholder;
        }
    }

    private const string Disclaimer =
        "PLACEHOLDER TEXT — this is generic boilerplate, not reviewed legal copy. " +
        "Replace with real Terms of Service / Privacy Policy content (ideally reviewed " +
        "by counsel, especially since this app supports Child profiles and stores " +
        "fitness, health, and photo data) before this app reaches real users.\n\n";

    private const string TermsPlaceholder = Disclaimer +
        "1. Acceptance of Terms\n" +
        "By creating an account or using Rig Ritual, you agree to these Terms of Service.\n\n" +
        "2. Accounts and Family Members\n" +
        "An account holder may add dependent family member profiles, including Child " +
        "profiles, and is responsible for the activity of every profile on their account.\n\n" +
        "3. Acceptable Use\n" +
        "Use the app only for its intended purpose of tracking workouts, nutrition, and " +
        "related fitness data for yourself and the family members you manage.\n\n" +
        "4. Subscriptions and Billing\n" +
        "Paid features are billed as described at time of purchase and may be changed with notice.\n\n" +
        "5. Termination\n" +
        "Either party may terminate an account as described in the app's account settings.\n\n" +
        "6. Disclaimer\n" +
        "This app is not a substitute for professional medical or fitness advice.\n\n" +
        "7. Changes to These Terms\n" +
        "These terms may be updated from time to time; continued use constitutes acceptance.";

    private const string PrivacyPlaceholder = Disclaimer +
        "1. Information We Collect\n" +
        "Account info (email, sign-in identity), fitness data you log (workouts, nutrition, " +
        "measurements, supplement/medication schedules), and photos you choose to upload " +
        "(profile picture, progress photos).\n\n" +
        "2. How We Use It\n" +
        "To provide the app's core functionality — syncing your data across your own devices " +
        "and displaying it back to you and the family members you've granted access to.\n\n" +
        "3. Children's Data\n" +
        "Child profiles are created and managed by the account holder (a parent/guardian), " +
        "who consents to this policy on the child's behalf.\n\n" +
        "4. Data Sharing\n" +
        "We do not sell your data. Data is shared only within your own family account, per " +
        "the access each member is granted.\n\n" +
        "5. Data Retention and Deletion\n" +
        "You can delete a member's data or your account from within the app's settings.\n\n" +
        "6. Security\n" +
        "Data is transmitted and stored using industry-standard practices.\n\n" +
        "7. Contact\n" +
        "Questions about this policy can be directed to the app's support contact.";
}
