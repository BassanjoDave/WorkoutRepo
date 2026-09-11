using WorkoutTracker.Models;

namespace WorkoutTracker.Services;

/// <summary>
/// Single seam for "does switching to/acting as this member require a PIN or
/// biometric scan right now?" — used both when tapping a profile tile
/// (ProfileGateViewModel.SelectMember) and before opening a
/// holder-administrative screen (ManageMembersViewModel/DevSettingsViewModel's
/// "settings lockdown"), so the None/Pin/Biometric branch lives in exactly one
/// place rather than four. DeviceAuthMode.None always returns true instantly —
/// this only prompts for members the primary holder has actually protected.
/// </summary>
public interface IMemberAuthGateService
{
    /// <summary>Returns true if the caller may proceed — either no protection is
    /// configured, or the user just passed it. Returns false on a cancelled
    /// prompt, a wrong PIN, or a failed/cancelled biometric scan.</summary>
    Task<bool> VerifyAsync(Member member, string reason);
}

public class MemberAuthGateService : IMemberAuthGateService
{
    private readonly IMemberPinService _pins;
    private readonly IBiometricAuthService _biometrics;

    public MemberAuthGateService(IMemberPinService pins, IBiometricAuthService biometrics)
    {
        _pins = pins;
        _biometrics = biometrics;
    }

    public async Task<bool> VerifyAsync(Member member, string reason)
    {
        switch (member.DeviceAuthMode)
        {
            case DeviceAuthMode.None:
                return true;

            case DeviceAuthMode.Pin:
                var page = Shell.Current?.CurrentPage;
                if (page is null) return false;
                var entered = await page.DisplayPromptAsync("Enter PIN", reason, keyboard: Keyboard.Numeric, maxLength: 8);
                if (entered is null) return false; // cancelled
                if (_pins.VerifyPin(member, entered)) return true;
                await page.DisplayAlertAsync("Incorrect PIN", "That PIN wasn't right.", "OK");
                return false;

            // A pure OS-level "is a legitimate device user present" check — it does
            // NOT resume any per-member Firebase session (there isn't one to resume;
            // SecureStorage only ever caches a single device-wide session). See the
            // per-member sign-in plan's "Locked decisions" for why this is
            // deliberate: it sidesteps needing per-identity session storage entirely.
            case DeviceAuthMode.Biometric:
                return await _biometrics.AuthenticateAsync(reason);

            default:
                return true;
        }
    }
}
