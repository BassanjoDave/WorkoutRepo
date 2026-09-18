using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Services;

/// <summary>
/// Mailbox letting the Ritual Builder's new sub-pages (Add Exercise, Browse
/// Exercises, Add From an Existing Ritual, Schedule &amp; Reminder) reach the
/// SAME live StandardBuilderViewModel/HiitBuilderViewModel instance the member
/// is already editing on the main builder page, instead of getting a fresh,
/// disconnected one from Shell's normal per-route DI resolution — mirrors the
/// existing IPendingExerciseBridge/IHomeWorkoutBridge singleton mailbox pattern.
/// Set by the builder page's OnAppearing; cleared by Save()/Cancel() when
/// leaving the builder flow entirely (not on every sub-page push/pop, since a
/// sub-page still needs it after the main page's own OnDisappearing fires).
/// </summary>
public interface IActiveRoutineBuilderContext
{
    StandardBuilderViewModel? ActiveStandard { get; set; }
    HiitBuilderViewModel? ActiveHiit { get; set; }
}

public class ActiveRoutineBuilderContext : IActiveRoutineBuilderContext
{
    public StandardBuilderViewModel? ActiveStandard { get; set; }
    public HiitBuilderViewModel? ActiveHiit { get; set; }
}
