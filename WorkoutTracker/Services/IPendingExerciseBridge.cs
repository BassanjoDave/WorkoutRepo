namespace WorkoutTracker.Services;

/// <summary>
/// Hands a newly-created exercise's id from ExerciseEditorViewModel back to
/// StandardBuilderViewModel after the Workout Builder's exercise picker's
/// "+ Add custom exercise…" entry sends the member there — mirrors
/// IHomeWorkoutBridge's identical problem for Home to the workout builders.
/// Set unconditionally on every exercise save; harmless when consumed by
/// nothing (e.g. the editor was opened from the exercise Library instead).
/// </summary>
public interface IPendingExerciseBridge
{
    void SetPendingExerciseId(Guid exerciseId);
    Guid? ConsumePendingExerciseId();
}

public class PendingExerciseBridge : IPendingExerciseBridge
{
    private Guid? _pendingExerciseId;

    public void SetPendingExerciseId(Guid exerciseId) => _pendingExerciseId = exerciseId;

    public Guid? ConsumePendingExerciseId()
    {
        var id = _pendingExerciseId;
        _pendingExerciseId = null;
        return id;
    }
}
