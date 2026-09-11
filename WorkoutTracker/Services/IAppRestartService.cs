namespace WorkoutTracker.Services;

/// <summary>Restarts the app in place (e.g. after a theme change, which only applies on next launch).</summary>
public interface IAppRestartService
{
    /// <summary>False on platforms (Android) with no reliable way to relaunch from inside the app.</summary>
    bool CanAutoRestart { get; }

    /// <summary>Starts a new instance of the app, then exits the current one. Only valid when CanAutoRestart is true.</summary>
    void Restart();
}

public class AppRestartService : IAppRestartService
{
#if WINDOWS
    public bool CanAutoRestart => true;

    public void Restart()
    {
        var exePath = Environment.ProcessPath;
        if (exePath is not null)
        {
            System.Diagnostics.Process.Start(exePath);
        }
        Environment.Exit(0);
    }
#else
    public bool CanAutoRestart => false;

    public void Restart()
    {
    }
#endif
}
