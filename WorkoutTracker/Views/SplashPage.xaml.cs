#if IOS
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
#endif

namespace WorkoutTracker.Views;

/// <summary>
/// The app's actual startup page (see App.xaml.cs's CreateWindow) — shows the
/// full-bleed hero photo edge to edge for a short, fixed dwell time, then swaps
/// the window's root page over to AppShell. Deliberately just a fixed delay
/// rather than waiting on IAppBootstrapper.EnsureSeedDataAsync() or session
/// restore: those already run exactly where they always have (inside
/// ProfileGateViewModel, once AppShell/its first page loads), and duplicating
/// that work here would mean two redundant repo round trips for no benefit —
/// this page's only job is to let the branded image actually be seen.
///
/// No ViewModel: there's no state to bind.
/// </summary>
public partial class SplashPage : ContentPage
{
    private static readonly TimeSpan DisplayDuration = TimeSpan.FromMilliseconds(1600);

    public SplashPage()
    {
        InitializeComponent();
#if IOS
        // Lets the image draw under the status bar / notch / home indicator instead
        // of being letterboxed inside the safe area — required for a genuine
        // edge-to-edge look on iOS. Android already draws full-bleed by default here.
        On<iOS>().SetUseSafeArea(false);
#endif
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Task.Delay(DisplayDuration);
        // Fully qualified: the iOSSpecific using above brings in its own
        // PlatformConfiguration.iOSSpecific.Application, which collides with
        // Microsoft.Maui.Controls.Application on a bare "Application" here.
        var app = Microsoft.Maui.Controls.Application.Current;
        if (app?.Windows.Count > 0)
        {
            app.Windows[0].Page = new AppShell();
        }
    }
}
