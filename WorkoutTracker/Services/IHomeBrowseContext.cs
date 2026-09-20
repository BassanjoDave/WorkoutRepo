namespace WorkoutTracker.Services;

/// <summary>
/// Mailbox carrying "which day was selected on Home" across the "Browse Workouts"
/// jump to the Rituals tab (an absolute `//exercise` tab switch, not a pushed route,
/// so there's no query-param path to carry this otherwise) — same consume-once
/// singleton pattern as IPendingExerciseBridge/IHomeWorkoutBridge. Lets WorkoutsView
/// ask "save to the day you were browsing from, or today?" when starting a routine
/// picked this way on a day other than today.
/// </summary>
public interface IHomeBrowseContext
{
    void SetPendingDate(DateOnly date);
    DateOnly? ConsumePendingDate();
}

public class HomeBrowseContext : IHomeBrowseContext
{
    private DateOnly? _pendingDate;

    public void SetPendingDate(DateOnly date) => _pendingDate = date;

    public DateOnly? ConsumePendingDate()
    {
        var date = _pendingDate;
        _pendingDate = null;
        return date;
    }
}
