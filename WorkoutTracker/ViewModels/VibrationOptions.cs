namespace WorkoutTracker.ViewModels;

/// <summary>Shared between the session runner and the routine builder — lifted from the design file's VIB_STANCES / VIB_MODES constants.</summary>
public static class VibrationOptions
{
    public static string[] Stances { get; } =
    {
        "Feet together (narrow)", "Shoulder-width", "Wide stance", "Staggered / split", "Single leg",
        "Balls of feet / toes", "Heels only", "Kneeling on plate", "Hands on plate", "Forearms on plate",
        "Seated on plate", "Seated, feet on plate", "Lying, feet on plate",
    };

    public static string[] Modes { get; } =
    {
        "Vibration (linear)", "Oscillation (pivotal)", "Tri-planar", "Combined / dual", "Massage pulse",
    };
}
