namespace WorkoutTracker.Services;

/// <summary>
/// The device's chosen visual theme — a device-level setting (via Preferences,
/// not per-member data) since it has to be known before any account/member is
/// selected, at the very first line of App.xaml.cs. Applied at startup only;
/// changing it takes effect on next launch, not live, so the whole app never
/// has to worry about resources changing out from under an already-rendered
/// page. To add a new theme: add an entry to Available and a matching
/// Resources/Styles/Colors.&lt;Id&gt;.xaml with the same keys as Colors.Dark.xaml.
/// </summary>
public static class ThemeSettings
{
    private const string PreferenceKey = "SelectedTheme";
    public const string DefaultTheme = "Light";

    public static readonly (string Id, string DisplayName)[] Available =
    {
        ("Dark", "Dark"),
        ("Light", "Light"),
    };

    public static string Current => Preferences.Default.Get(PreferenceKey, DefaultTheme);

    public static void Set(string themeId) => Preferences.Default.Set(PreferenceKey, themeId);
}
