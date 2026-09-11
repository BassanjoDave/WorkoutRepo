namespace WorkoutTracker.Services.Storage;

public enum SyncState { Idle, Syncing, Offline }

public interface ISyncStatusService
{
    SyncState State { get; }
    int PendingCount { get; }
    string? LastError { get; }
    event Action? Changed;
    void Report(SyncState state, int pendingCount, string? lastError = null);
}

public class SyncStatusService : ISyncStatusService
{
    public SyncState State { get; private set; } = SyncState.Idle;
    public int PendingCount { get; private set; }
    public string? LastError { get; private set; }
    public event Action? Changed;

    public void Report(SyncState state, int pendingCount, string? lastError = null)
    {
        State = state;
        PendingCount = pendingCount;
        if (lastError is not null) LastError = lastError;
        if (state == SyncState.Idle) LastError = null;
        Changed?.Invoke();
    }
}
