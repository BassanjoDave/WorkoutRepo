namespace WorkoutTracker.Resources.Styles;

// "Nocturne" — the original dark theme from the design handoff, unchanged.
// Plain C# rather than XAML: constructing an x:Class'd ResourceDictionary
// from code (`new ColorsDark()` calling a XAML-generated InitializeComponent)
// crashed the app outright (0xC0000135) under this project's SourceGen XAML
// compilation, even though the exact same file loaded fine when statically
// declared in App.xaml's MergedDictionaries. Building the dictionary by hand
// here sidesteps XAML compilation entirely for the one case that actually
// needs to be instantiated at runtime. See App.xaml.cs and Services/ThemeSettings.cs.
public sealed class ColorsDark : ResourceDictionary
{
    public ColorsDark()
    {
        // Ground
        Add("ColorBg", Color.FromArgb("#161826"));
        Add("ColorSurface", Color.FromArgb("#232532"));
        Add("ColorText", Color.FromArgb("#E9E9ED"));
        Add("ColorAccent", Color.FromArgb("#9184D9"));
        Add("ColorAccent2", Color.FromArgb("#A7A1DB"));
        // CSS uses color-mix(#e9e9ed 16%, transparent) for the divider; flattened against ColorBg.
        Add("ColorDivider", Color.FromArgb("#33353F"));

        // Brushes
        Add("BgBrush", new SolidColorBrush(Color.FromArgb("#161826")));
        Add("SurfaceBrush", new SolidColorBrush(Color.FromArgb("#232532")));
        Add("TextBrush", new SolidColorBrush(Color.FromArgb("#E9E9ED")));
        Add("AccentBrush", new SolidColorBrush(Color.FromArgb("#9184D9")));
        Add("DividerBrush", new SolidColorBrush(Color.FromArgb("#33353F")));
        Add("MutedTextBrush", new SolidColorBrush(Color.FromArgb("#9397AB"))); // Base ColorNeutral500
        Add("AccentTagBg", new SolidColorBrush(Color.FromArgb("#423A6A"))); // Base ColorAccent800
        Add("AccentTagText", new SolidColorBrush(Color.FromArgb("#F5F4FF"))); // Base ColorAccent100

        // Brand banner (Home page header) — a step brighter than the light theme's navy
        // so it still reads as a distinct blue banner rather than receding into the dark
        // background; white text stays the contrasting choice in both themes.
        Add("ColorBanner", Color.FromArgb("#2E6DA8"));
        Add("ColorBannerText", Color.FromArgb("#FFFFFF"));

        // Shadows — a purple backlit glow instead of a literal drop shadow: a zero offset
        // (light isn't "falling" from anywhere, it's emanating from behind/around the
        // surface) using the accent hue, which reads better than a black shadow against
        // an already-dark background. Kept deliberately faint on ShadowSm specifically —
        // it's used on selectable chips/buttons throughout the app, where accent coloring
        // means "selected"; a strong glow on every chip made unselected ones look active too.
        Add("ShadowSm", new Shadow { Brush = new SolidColorBrush(Color.FromArgb("#9184D9")), Opacity = 0.25f, Radius = 6, Offset = new Point(0, 0) });
        Add("ShadowMd", new Shadow { Brush = new SolidColorBrush(Color.FromArgb("#9184D9")), Opacity = 0.60f, Radius = 20, Offset = new Point(0, 0) });
        Add("ShadowLg", new Shadow { Brush = new SolidColorBrush(Color.FromArgb("#9184D9")), Opacity = 0.65f, Radius = 36, Offset = new Point(0, 0) });
    }
}
