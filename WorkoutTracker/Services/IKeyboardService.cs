#if ANDROID
using Android.Content;
using Android.Views.InputMethods;
using Microsoft.Maui.ApplicationModel;
#elif IOS || MACCATALYST
using UIKit;
#endif

namespace WorkoutTracker.Services;

/// <summary>
/// Explicitly dismisses the on-screen keyboard. Needed because hiding a
/// focused Entry's container via an IsVisible binding — e.g. ProfileGateViewModel's
/// sign-in form collapsing once IsUnlocked flips true — does not reliably
/// dismiss the soft keyboard on its own; the OS doesn't automatically clear
/// focus/hide the IME just because the control holding it became invisible.
/// Confirmed reproducing on Android: the keyboard stayed up over the profile
/// tile list after a successful sign-in until manually dismissed.
/// </summary>
public interface IKeyboardService
{
    void HideKeyboard();
}

public class KeyboardService : IKeyboardService
{
#if ANDROID
    public void HideKeyboard()
    {
        var activity = Platform.CurrentActivity;
        var focused = activity?.CurrentFocus;
        if (activity is null || focused is null) return;

        if (activity.GetSystemService(Context.InputMethodService) is InputMethodManager imm)
        {
            imm.HideSoftInputFromWindow(focused.WindowToken, HideSoftInputFlags.None);
        }
        focused.ClearFocus();
    }
#elif IOS || MACCATALYST
    public void HideKeyboard() => UIApplication.SharedApplication.KeyWindow?.EndEditing(true);
#else
    // Windows/other platforms don't exhibit this persistent-on-screen-keyboard
    // issue the same way — nothing to do.
    public void HideKeyboard() { }
#endif
}
