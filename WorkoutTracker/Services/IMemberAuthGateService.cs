using WorkoutTracker.Models;
using WorkoutTracker.Services.Storage;

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

    /// <summary>Prompts for and sets a brand-new PIN on `member` (two chained
    /// numeric dialogs: enter, then confirm) — does not persist anything itself,
    /// callers save the owning AccountIndex once this returns true. Shared by
    /// MemberEditViewModel's own PIN-management UI and by VerifyOrEstablishAsync
    /// below, which is the only other place a member's DeviceAuthMode changes.</summary>
    Task<bool> PromptAndSetPinAsync(Member member);

    /// <summary>Like VerifyAsync, but for DeviceAuthMode.None it no longer just
    /// says "fine, proceed" — every member is now required to have a PIN, so
    /// this forces setup right then (an explanatory alert, then
    /// PromptAndSetPinAsync, then a save) instead of silently letting an
    /// unprotected member (most dangerously, an unprotected Owner) be switched
    /// into or administered. Returns false if setup is cancelled — the caller
    /// (profile switch, or a "settings lockdown" screen) should treat that the
    /// same as a failed PIN/biometric check.</summary>
    Task<bool> VerifyOrEstablishAsync(Member member, AccountIndex accountIndex, IWorkoutRepository repo, string reason);
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

    public async Task<bool> PromptAndSetPinAsync(Member member)
    {
        var page = Shell.Current?.CurrentPage;
        if (page is null) return false;

        var pin = await page.DisplayPromptAsync("Set PIN", $"Choose a PIN for {member.DisplayName} (4-8 digits)", keyboard: Keyboard.Numeric, maxLength: 8);
        if (string.IsNullOrEmpty(pin)) return false; // cancelled
        if (pin.Length < 4 || !pin.All(char.IsDigit))
        {
            await page.DisplayAlertAsync("Invalid PIN", "PIN must be 4-8 digits.", "OK");
            return false;
        }

        var confirm = await page.DisplayPromptAsync("Confirm PIN", "Enter the same PIN again", keyboard: Keyboard.Numeric, maxLength: 8);
        if (confirm != pin)
        {
            await page.DisplayAlertAsync("PINs didn't match", "Try again.", "OK");
            return false;
        }

        _pins.SetPin(member, pin);
        return true;
    }

    public async Task<bool> VerifyOrEstablishAsync(Member member, AccountIndex accountIndex, IWorkoutRepository repo, string reason)
    {
        if (member.DeviceAuthMode != DeviceAuthMode.None) return await VerifyAsync(member, reason);

        var page = Shell.Current?.CurrentPage;
        if (page is null) return false;
        await page.DisplayAlertAsync("PIN required", $"{member.DisplayName}'s profile isn't protected yet. Set a PIN to continue.", "OK");
        if (!await PromptAndSetPinAsync(member)) return false;

        member.DeviceAuthMode = DeviceAuthMode.Pin;
        await repo.SaveAccountIndexAsync(accountIndex);
        return true;
    }
}
