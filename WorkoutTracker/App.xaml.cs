using Microsoft.Extensions.DependencyInjection;
using WorkoutTracker.Resources.Styles;
using WorkoutTracker.Services;
using WorkoutTracker.Views;

namespace WorkoutTracker;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		// Colors.Base.xaml (the ramps) and Icons.xaml are already merged via App.xaml's
		// own static MergedDictionaries, set up by the InitializeComponent() call above.
		// This adds whichever theme's ground palette + Shadows (bg/surface/text/accent/
		// divider/ShadowSm/Md/Lg) the member last chose, then merges Styles.xaml — which
		// must come AFTER the theme dictionary, since Styles.xaml's own Setters reference
		// those keys via plain StaticResource and are parsed the moment it's merged.
		// ColorsDark/ColorsLight are plain C# ResourceDictionary subclasses
		// (Resources/Styles/Colors.Dark.cs, Colors.Light.cs), not XAML. Add a new theme
		// by adding a case here alongside its entry in ThemeSettings.Available.
		ResourceDictionary themeColors = ThemeSettings.Current switch
		{
			"Light" => new ColorsLight(),
			_ => new ColorsDark(),
		};
		Resources.MergedDictionaries.Add(themeColors);
		Resources.MergedDictionaries.Add(new AppStyles());
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// Starts on SplashPage, not AppShell directly — it shows the full-bleed hero
		// photo for a moment, then swaps itself out for a real AppShell. See
		// SplashPage.xaml.cs.
		var window = new Window(new SplashPage());

#if WINDOWS
		// The design's reference viewport is a 390x844 phone frame. Desktop windows
		// aren't constrained to that by default, so left alone the layout stretches
		// into a wide, phone-shaped-content-in-a-wide-window look. Keep it phone-ish
		// but still resizable within a sane range, rather than the default unbounded width.
		window.Width = 420;
		window.Height = 900;
		window.MinimumWidth = 360;
		window.MaximumWidth = 500;
		window.MinimumHeight = 700;
#endif

		return window;
	}
}