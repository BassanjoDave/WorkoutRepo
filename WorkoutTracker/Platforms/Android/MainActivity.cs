using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using WorkoutTracker.Services;

namespace WorkoutTracker;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Checked and copied to the clipboard here, before base.OnCreate() runs
        // MAUI's own startup — if the same crash reproduces every launch, this
        // is the only point guaranteed to run first regardless.
        var crash = CrashLogger.TakeLastCrash();
        if (crash is not null)
        {
            try
            {
                var clipboard = (ClipboardManager?)GetSystemService(ClipboardService);
                if (clipboard is not null) clipboard.PrimaryClip = ClipData.NewPlainText("Last crash", crash);
            }
            catch { /* best effort — the toast below is the fallback */ }

            Toast.MakeText(this, "App crashed last launch — details copied to clipboard. Paste them to report it.", ToastLength.Long)?.Show();
        }

        base.OnCreate(savedInstanceState);
    }
}
