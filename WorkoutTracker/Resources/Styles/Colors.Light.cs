namespace WorkoutTracker.Resources.Styles;

// Light theme — same accent hue family as Dark (Colors.Dark.cs), same
// invariant ramps (Colors.Base.xaml), with the ground roles inverted and the
// accent shifted a step darker on the existing Accent ramp (Accent600/700
// instead of the raw ColorAccent/ColorAccent2 values) so it still reads
// clearly against a light background instead of the dark one it was tuned for.
// Not part of the original design handoff — a first pass, not a certified
// contrast/accessibility audit. Plain C# rather than XAML — see Colors.Dark.cs
// for why.
public sealed class ColorsLight : ResourceDictionary
{
    public ColorsLight()
    {
        // Ground
        Add("ColorBg", Color.FromArgb("#F7F7FB"));
        Add("ColorSurface", Color.FromArgb("#FFFFFF"));
        Add("ColorText", Color.FromArgb("#1C1D26"));
        Add("ColorAccent", Color.FromArgb("#796CBF"));
        Add("ColorAccent2", Color.FromArgb("#5D5294"));
        Add("ColorDivider", Color.FromArgb("#E1E2EC"));

        // Overrides the Base ramp's ColorNeutral500 (#9397AB) — Styles.xaml uses
        // that step directly for meta/kicker text and placeholders, and it's too
        // low-contrast against a light background even though it read fine on
        // dark. Same hex as the ramp's own Neutral700 step.
        Add("ColorNeutral500", Color.FromArgb("#595D6C"));

        // Brushes
        Add("BgBrush", new SolidColorBrush(Color.FromArgb("#F7F7FB")));
        Add("SurfaceBrush", new SolidColorBrush(Color.FromArgb("#FFFFFF")));
        Add("TextBrush", new SolidColorBrush(Color.FromArgb("#1C1D26")));
        Add("AccentBrush", new SolidColorBrush(Color.FromArgb("#796CBF")));
        Add("DividerBrush", new SolidColorBrush(Color.FromArgb("#E1E2EC")));
        Add("MutedTextBrush", new SolidColorBrush(Color.FromArgb("#595D6C")));
        Add("AccentTagBg", new SolidColorBrush(Color.FromArgb("#423A6A"))); // Base ColorAccent800
        Add("AccentTagText", new SolidColorBrush(Color.FromArgb("#F5F4FF"))); // Base ColorAccent100

        // Brand banner (Home page header) — the Rig Ritual logo's navy/blue, distinct
        // from the app's own purple ColorAccent family used everywhere else. White text
        // reads as the clear contrasting choice against either theme's banner shade.
        Add("ColorBanner", Color.FromArgb("#1D4E89"));
        Add("ColorBannerText", Color.FromArgb("#FFFFFF"));

        // Shadows — a conventional soft dark drop shadow; Dark theme uses a purple glow
        // instead (see Colors.Dark.cs), which wouldn't read as "elevation" on a light
        // background the way a dark shadow does.
        Add("ShadowSm", new Shadow { Brush = new SolidColorBrush(Colors.Black), Opacity = 0.30f, Radius = 8, Offset = new Point(0, 2) });
        Add("ShadowMd", new Shadow { Brush = new SolidColorBrush(Colors.Black), Opacity = 0.45f, Radius = 18, Offset = new Point(0, 6) });
        Add("ShadowLg", new Shadow { Brush = new SolidColorBrush(Colors.Black), Opacity = 0.55f, Radius = 40, Offset = new Point(0, 16) });
    }
}
