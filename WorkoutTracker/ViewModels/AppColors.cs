namespace WorkoutTracker.ViewModels;

/// <summary>Looks up a Nocturne color token from the merged resource dictionaries, so view models stay the single source of truth's consumer rather than duplicating hex values.</summary>
internal static class AppColors
{
    public static Color Get(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
            ? color
            : Colors.Magenta;
}
