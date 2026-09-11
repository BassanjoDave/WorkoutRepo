namespace WorkoutTracker.Services;

/// <summary>
/// Hands a newly-created routine's id from StandardBuilderViewModel/HiitBuilderViewModel
/// back to HomeViewModel after "+ Add Workout" → "Create a new workout" sends the member
/// there — mirrors IRecipeDraftBridge's identical problem for Recipe Builder to Food
/// Editor. Only set when the builder was opened from Home (fromHome=true query flag) and
/// the save actually created a brand-new routine, not when editing an existing one.
/// </summary>
public interface IHomeWorkoutBridge
{
    void SetPendingRoutineId(Guid routineId);
    Guid? ConsumePendingRoutineId();
}

public class HomeWorkoutBridge : IHomeWorkoutBridge
{
    private Guid? _pendingRoutineId;

    public void SetPendingRoutineId(Guid routineId) => _pendingRoutineId = routineId;

    public Guid? ConsumePendingRoutineId()
    {
        var id = _pendingRoutineId;
        _pendingRoutineId = null;
        return id;
    }
}
