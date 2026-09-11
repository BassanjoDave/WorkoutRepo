namespace WorkoutTracker.Services;

/// <summary>
/// The countdown cue at 3/2/1 seconds. Uses haptic feedback (built into MAUI
/// Essentials, no extra dependency) rather than a synthesized tone — pulling
/// in a full audio-player package (CommunityToolkit.Maui drags in the entire
/// WindowsAppSDK on Windows, hundreds of MB) for a 0.16-second beep isn't a
/// reasonable tradeoff. A real tone is a fine later upgrade if the audio
/// dependency turns out to be worth it once other features need it too.
/// </summary>
public interface IHiitSoundService
{
    void PlayBeep(bool suppressed);
}

public class HiitSoundService : IHiitSoundService
{
    public void PlayBeep(bool suppressed)
    {
        if (suppressed) return;
        try
        {
            HapticFeedback.Default.Perform(HapticFeedbackType.Click);
        }
        catch
        {
            // No haptics support on this device/platform — the countdown UI still works without it.
        }
    }
}
