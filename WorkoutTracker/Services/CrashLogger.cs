namespace WorkoutTracker.Services;

/// <summary>
/// Last-resort crash capture for diagnosing startup crashes on a device we
/// have no debugger/logcat access to. Registered as the very first thing
/// MauiProgram.CreateMauiApp() does, so it catches exceptions thrown during
/// MAUI's own startup, not just ones from app code running after the first
/// page is up. Writes to plain BCL file APIs (not FileSystem.AppDataDirectory)
/// specifically because Maui Essentials isn't guaranteed initialized yet this
/// early — Environment.GetFolderPath works regardless.
/// </summary>
public static class CrashLogger
{
    private static readonly string LogPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "last-crash.log");

    public static void Install()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            TryWrite(e.ExceptionObject as Exception, "AppDomain.UnhandledException");

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            TryWrite(e.Exception, "TaskScheduler.UnobservedTaskException");
            e.SetObserved();
        };
    }

    private static void TryWrite(Exception? ex, string source)
    {
        try
        {
            var text = $"{DateTimeOffset.Now:O} — {source}\n{ex}\n";
            File.WriteAllText(LogPath, text);
        }
        catch
        {
            // If we can't even write the crash log, there's nothing more to do —
            // the process is already going down.
        }
    }

    /// <summary>Returns the last crash's text and deletes the file, or null if there wasn't one. Call once, early, on the next successful launch.</summary>
    public static string? TakeLastCrash()
    {
        try
        {
            if (!File.Exists(LogPath)) return null;
            var text = File.ReadAllText(LogPath);
            File.Delete(LogPath);
            return text;
        }
        catch
        {
            return null;
        }
    }
}
