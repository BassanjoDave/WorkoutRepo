using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.ViewModels;

public partial class ProfileViewModel : ObservableObject
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;
    private readonly IAppRestartService _appRestart;
    private readonly RemoteApiWorkoutRepository _remoteRepo;
    private readonly IBiometricAuthService _biometrics;
    private readonly IFirebaseAuthService _firebaseAuth;
    private bool _suppressBiometricToggleHandler;

    [ObservableProperty] public partial string ActiveMemberName { get; set; } = "";
    [ObservableProperty] public partial string ActiveMemberInitial { get; set; } = "";
    [ObservableProperty] public partial Color ActiveMemberColor { get; set; } = Colors.Gray;
    [ObservableProperty] public partial bool ActiveMemberUsesPhoto { get; set; }
    [ObservableProperty] public partial ImageSource? ActiveMemberPhoto { get; set; }
    [ObservableProperty] public partial bool IsLbs { get; set; } = true;
    [ObservableProperty] public partial bool CanManageMembers { get; set; }
    /// <summary>True when the active member still has DeviceAuthMode.None — shows a
    /// courtesy banner nudging them to set a PIN. Purely a nudge: the actual
    /// enforcement backstop is IMemberAuthGateService.VerifyOrEstablishAsync, which
    /// forces setup at the moment of risk (a profile switch or an admin screen)
    /// regardless of whether this banner was ever seen or acted on.</summary>
    [ObservableProperty] public partial bool NeedsPinSetup { get; set; }
    /// <summary>Courtesy nudge mirroring NeedsPinSetup — true when the device's
    /// currently signed-in identity has an unverified email (a Google/Apple sign-in
    /// is always already verified, so this only really fires for a password account
    /// that hasn't clicked its verification link yet).</summary>
    [ObservableProperty] public partial bool NeedsEmailVerification { get; set; }
    [ObservableProperty] public partial string EmailVerificationStatus { get; set; } = "Please verify your email address";
    /// <summary>Only the primary holder can delete the whole account — everyone
    /// else's way out is Manage Members' existing "Remove" action, which only
    /// affects their own membership, not the family's shared data.</summary>
    [ObservableProperty] public partial bool IsPrimaryHolder { get; set; }
    [ObservableProperty] public partial ObservableCollection<ScheduleSummaryRowViewModel> ScheduleRows { get; set; } = new();
    [ObservableProperty] public partial ObservableCollection<ThemeOptionViewModel> ThemeOptions { get; set; } = new();

    /// <summary>Hides the whole biometric-sign-in row on hardware/platforms that
    /// can't do it at all, rather than showing a toggle that would never work.</summary>
    [ObservableProperty] public partial bool BiometricUnlockAvailable { get; set; }
    [ObservableProperty] public partial bool BiometricUnlockEnabled { get; set; }

    public ProfileViewModel(IActiveSessionService session, IWorkoutRepository repo, IAppRestartService appRestart,
        RemoteApiWorkoutRepository remoteRepo, IBiometricAuthService biometrics, IFirebaseAuthService firebaseAuth)
    {
        _session = session;
        _repo = repo;
        _appRestart = appRestart;
        _remoteRepo = remoteRepo;
        _biometrics = biometrics;
        _firebaseAuth = firebaseAuth;
    }

    public async Task LoadAsync()
    {
        var account = _session.ActiveAccount;
        var member = _session.ActiveMember;
        if (account is null || member is null)
        {
            await Shell.Current.GoToAsync("//gate");
            return;
        }

        ActiveMemberName = member.DisplayName;
        ActiveMemberInitial = member.Initial;
        ActiveMemberColor = Color.FromArgb(member.AvatarColor);
        ActiveMemberUsesPhoto = member.AvatarDisplay == AvatarDisplay.Photo && member.AvatarPhotoBlobFileName is not null;
        if (ActiveMemberUsesPhoto)
        {
            var blobFileName = member.AvatarPhotoBlobFileName!;
            ActiveMemberPhoto = ImageSource.FromStream(async _ =>
            {
                var bytes = await _repo.GetProgressPhotoBlobAsync(account.Id, member.Id, blobFileName);
                return bytes is null ? null : new MemoryStream(bytes);
            });
        }
        CanManageMembers = member.EffectiveCapabilities().ManageMembers;
        IsPrimaryHolder = member.Id == account.PrimaryHolderMemberId;
        NeedsPinSetup = member.DeviceAuthMode == DeviceAuthMode.None;
        NeedsEmailVerification = !await _firebaseAuth.IsEmailVerifiedAsync();

        BiometricUnlockAvailable = _biometrics.IsSupported && await _biometrics.IsAvailableAsync();
        _suppressBiometricToggleHandler = true;
        BiometricUnlockEnabled = Preferences.Default.Get(ProfileGateViewModel.BiometricUnlockPrefKey, false);
        _suppressBiometricToggleHandler = false;

        var shared = await _repo.GetSharedLibraryAsync(account.Id);
        var manufacturer = await _repo.GetManufacturerLibraryAsync();
        RoutineDefinition? FindRoutine(Guid id) =>
            shared.Routines.FirstOrDefault(r => r.Id == id) ?? manufacturer.Routines.FirstOrDefault(r => r.Id == id);

        var memberData = await _repo.GetMemberDataAsync(account.Id, member.Id);
        IsLbs = memberData.WeightUnit != "kg";

        // Replacing the whole collection (rather than Clear() + Add() in place) avoids
        // BindableLayout briefly seeing an empty source mid-rebuild — that transient
        // empty state crashes natively inside WinUI's own child-collection handling
        // (see the same fix in WorkoutsViewModel.Rebuild()).
        var scheduleRows = new ObservableCollection<ScheduleSummaryRowViewModel>();
        foreach (Weekday day in Enum.GetValues<Weekday>())
        {
            memberData.Schedule.Days.TryGetValue(day, out var slots);
            var amNames = (slots?.Am ?? new()).Select(id => FindRoutine(id)?.Name).Where(n => n is not null);
            var pmNames = (slots?.Pm ?? new()).Select(id => FindRoutine(id)?.Name).Where(n => n is not null);
            var parts = amNames.Select(n => $"{n} (AM)").Concat(pmNames.Select(n => $"{n} (PM)")).ToList();
            scheduleRows.Add(new ScheduleSummaryRowViewModel(day.ToString()[..3], parts.Count > 0 ? string.Join(", ", parts) : "Rest"));
        }
        ScheduleRows = scheduleRows;

        var currentTheme = ThemeSettings.Current;
        var themeOptions = new ObservableCollection<ThemeOptionViewModel>();
        foreach (var (id, name) in ThemeSettings.Available)
        {
            themeOptions.Add(new ThemeOptionViewModel(id, name, id == currentTheme, SelectThemeCommand));
        }
        ThemeOptions = themeOptions;
    }

    /// <summary>Turning this ON requires an actual successful biometric scan first —
    /// proves the enrolled Face ID/fingerprint really belongs to whoever's flipping
    /// the switch, not just that they could reach this screen. Turning it OFF never
    /// needs confirmation. The suppression flag keeps LoadAsync's own initial
    /// assignment (reflecting whatever's already stored) from re-triggering this.</summary>
    partial void OnBiometricUnlockEnabledChanged(bool value)
    {
        if (_suppressBiometricToggleHandler) return;
        _ = HandleBiometricToggleAsync(value);
    }

    private async Task HandleBiometricToggleAsync(bool enable)
    {
        if (!enable)
        {
            Preferences.Default.Set(ProfileGateViewModel.BiometricUnlockPrefKey, false);
            return;
        }

        var confirmed = await _biometrics.AuthenticateAsync("Confirm to turn on biometric sign-in");
        if (!confirmed)
        {
            _suppressBiometricToggleHandler = true;
            BiometricUnlockEnabled = false;
            _suppressBiometricToggleHandler = false;
            return;
        }
        Preferences.Default.Set(ProfileGateViewModel.BiometricUnlockPrefKey, true);
    }

    /// <summary>Device-level, not per-member — only takes effect on the next launch (see ThemeSettings),
    /// so this offers to restart the app immediately rather than leaving the change silently pending.</summary>
    [RelayCommand]
    private async Task SelectTheme(string themeId)
    {
        if (themeId == ThemeSettings.Current) return;

        ThemeSettings.Set(themeId);
        foreach (var option in ThemeOptions) option.IsSelected = option.Id == themeId;
        var themeName = ThemeOptions.FirstOrDefault(o => o.Id == themeId)?.DisplayName ?? themeId;

        if (_appRestart.CanAutoRestart)
        {
            var restartNow = await Shell.Current.CurrentPage.DisplayAlertAsync(
                "Restart to apply theme",
                $"Restart Rig Ritual now to switch to {themeName}?",
                "Restart", "Later");
            if (restartNow) _appRestart.Restart();
        }
        else
        {
            await Shell.Current.CurrentPage.DisplayAlertAsync(
                "Theme saved",
                $"{themeName} will apply the next time you open Rig Ritual.",
                "OK");
        }
    }

    [RelayCommand]
    private async Task SetUnitLbs()
    {
        IsLbs = true;
        await PersistUnitAsync();
    }

    [RelayCommand]
    private async Task SetUnitKg()
    {
        IsLbs = false;
        await PersistUnitAsync();
    }

    private async Task PersistUnitAsync()
    {
        var account = _session.ActiveAccount;
        var member = _session.ActiveMember;
        if (account is null || member is null) return;
        var memberData = await _repo.GetMemberDataAsync(account.Id, member.Id);
        memberData.WeightUnit = IsLbs ? "lbs" : "kg";
        await _repo.SaveMemberDataAsync(account.Id, member.Id, memberData);
    }

    [RelayCommand]
    private async Task ResendVerificationEmail()
    {
        var page = Shell.Current?.CurrentPage;
        var idToken = await _firebaseAuth.GetValidIdTokenAsync();
        if (idToken is null) return;

        var sent = await _firebaseAuth.SendEmailVerificationAsync(idToken);
        EmailVerificationStatus = sent
            ? "Verification email sent — check your inbox."
            : "Please verify your email address";
        if (page is not null)
        {
            await page.DisplayAlertAsync(sent ? "Email sent" : "Couldn't send it",
                sent ? "Check your inbox for the verification link." : _firebaseAuth.LastError ?? "Something went wrong.", "OK");
        }
    }

    [RelayCommand]
    private async Task OpenScheduleEditor() => await Shell.Current.GoToAsync("scheduleEditor");

    [RelayCommand]
    private async Task OpenManageMembers() => await Shell.Current.GoToAsync("manageMembers");

    [RelayCommand]
    private async Task OpenEquipmentPreferences() => await Shell.Current.GoToAsync("equipmentPreferences");

    [RelayCommand]
    private async Task OpenMyRigs() => await Shell.Current.GoToAsync("myRigs");

    [RelayCommand]
    private async Task OpenReminders() => await Shell.Current.GoToAsync("reminders");

    [RelayCommand]
    private async Task OpenMeasurements() => await Shell.Current.GoToAsync("//measurements");

    [RelayCommand]
    private async Task OpenCustomizeHome() => await Shell.Current.GoToAsync("customizeHome");

    [RelayCommand]
    private async Task OpenProfileGate() => await Shell.Current.GoToAsync("//gate");

    /// <summary>Edits the signed-in member's own name/avatar/details — reuses
    /// MemberEditPage directly rather than a separate screen, and bypasses the
    /// ManageMembers gate that Manage Members' own edit entry requires, since
    /// editing yourself should never depend on that permission.</summary>
    [RelayCommand]
    private async Task OpenEditProfile()
    {
        var member = _session.ActiveMember;
        if (member is null) return;
        await Shell.Current.GoToAsync($"memberEdit?mode=edit&memberId={member.Id}");
    }

    /// <summary>
    /// Permanently deletes the whole account server-side (see Program.cs's DELETE
    /// /accounts/{accountId} and RemoteApiWorkoutRepository.DeleteAccountAsync) —
    /// every member, every workout/nutrition/stack record, gone, and the identity
    /// directory scrubbed so a future sign-in can't find it again. This is the
    /// in-app deletion path app store review requires and the privacy policy's
    /// "contact us to delete your data" promise. Two-step confirmation (a warning,
    /// then typing DELETE) since there's no undo once the server call succeeds.
    /// </summary>
    [RelayCommand]
    private async Task DeleteAccount()
    {
        var account = _session.ActiveAccount;
        var member = _session.ActiveMember;
        if (account is null || member is null || Shell.Current?.CurrentPage is not Page page) return;

        var proceed = await page.DisplayAlertAsync("Delete account",
            "This permanently deletes your entire account — every member, every workout, meal, measurement, and supplement log. This cannot be undone.",
            "Continue", "Cancel");
        if (!proceed) return;

        var typed = await page.DisplayPromptAsync("Confirm deletion",
            "Type DELETE to permanently delete this account.", "Delete", "Cancel");
        if (!string.Equals(typed?.Trim(), "DELETE", StringComparison.Ordinal)) return;

        try
        {
            await _remoteRepo.DeleteAccountAsync(account.Id);
        }
        catch (Exception ex)
        {
            await page.DisplayAlertAsync("Couldn't delete account", $"The account wasn't deleted: {ex.Message}", "OK");
            return;
        }

        var deviceIndex = await _repo.GetDeviceIndexAsync();
        deviceIndex.AccountIds.Remove(account.Id);
        if (deviceIndex.ActiveAccountId == account.Id)
        {
            deviceIndex.ActiveAccountId = null;
            deviceIndex.ActiveMemberId = null;
        }
        await _repo.SaveDeviceIndexAsync(deviceIndex);

        await _session.ClearAsync();
        await Shell.Current!.GoToAsync("//gate");
    }
}

public partial class ThemeOptionViewModel : ObservableObject
{
    public string Id { get; }
    public string DisplayName { get; }
    public IRelayCommand<string> SelectCommand { get; }

    [ObservableProperty] public partial bool IsSelected { get; set; }

    public ThemeOptionViewModel(string id, string displayName, bool isSelected, IRelayCommand<string> selectCommand)
    {
        Id = id;
        DisplayName = displayName;
        IsSelected = isSelected;
        SelectCommand = selectCommand;
    }
}

public class ScheduleSummaryRowViewModel
{
    public string Day { get; }
    public string Summary { get; }

    public ScheduleSummaryRowViewModel(string day, string summary)
    {
        Day = day;
        Summary = summary;
    }
}
