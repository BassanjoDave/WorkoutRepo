using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// Add-dependent and edit-member share this screen. Capability checkboxes
/// start from the selected preset and only become an override when the
/// holder actually changes one — matching Member.Overrides' sparse-override
/// design, so a member who's never been customized keeps tracking preset
/// changes for free.
/// </summary>
public partial class MemberEditViewModel : ObservableObject, IQueryAttributable
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;
    private readonly ISeatAvailabilityService _seats;
    private readonly IGoogleAuthService _googleAuth;
    private readonly IFirebaseAuthService _firebaseAuth;
    private readonly IIdentityService _identity;
    private readonly IMemberPinService _pins;

    private bool _isNew;
    private Guid? _memberId;
    private AccountIndex? _accountIndex;
    private bool _isLoadingAuthMode;
    private DeviceAuthMode _lastAppliedAuthMode = DeviceAuthMode.None;

    // A static field so the SelectedColor default below can reference it in a field
    // initializer; exposed as an instance property underneath since XAML data
    // binding only resolves properties, not fields.
    private static readonly string[] AvatarColors =
        { "#8085DC", "#00A6AE", "#C36DA8", "#A29015", "#D26E5E", "#31A773" };
    public string[] AvatarColorOptions => AvatarColors;

    [ObservableProperty] public partial string Title { get; set; } = "Add Member";
    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial string SelectedColor { get; set; } = AvatarColors[0];

    /// <summary>Drives the live avatar preview at the top of the page — kept in sync
    /// with Name rather than recomputed in XAML, since there's no first-letter
    /// binding converter in use elsewhere for this.</summary>
    public string PreviewInitial => string.IsNullOrWhiteSpace(Name) ? "?" : Name.Trim()[..1].ToUpperInvariant();
    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(PreviewInitial));
    [ObservableProperty] public partial DateTime DateOfBirth { get; set; } = DateTime.Today.AddYears(-10);
    [ObservableProperty] public partial bool HasDateOfBirth { get; set; }
    [ObservableProperty] public partial RolePreset SelectedRole { get; set; } = RolePreset.Adult;

    [ObservableProperty] public partial bool ViewOthersHistory { get; set; }
    [ObservableProperty] public partial bool CreateSharedWorkouts { get; set; }
    [ObservableProperty] public partial bool EditOthersWorkouts { get; set; }
    [ObservableProperty] public partial bool EditOwnSchedule { get; set; }
    [ObservableProperty] public partial bool EditAnyoneSchedule { get; set; }
    [ObservableProperty] public partial bool DeleteContent { get; set; }
    [ObservableProperty] public partial bool ManageMembers { get; set; }
    [ObservableProperty] public partial bool ManageBilling { get; set; }
    [ObservableProperty] public partial bool ExportData { get; set; }

    public ObservableCollection<RolePreset> RoleOptions { get; } = new(Enum.GetValues<RolePreset>());

    [ObservableProperty] public partial bool CanLinkGoogle { get; set; }
    [ObservableProperty] public partial bool IsLinked { get; set; }
    [ObservableProperty] public partial bool IsLinking { get; set; }
    [ObservableProperty] public partial string LinkedGoogleLabel { get; set; } = "Not linked";

    /// <summary>True when the current viewer may change this member's DeviceAuthMode/PIN —
    /// a member can always manage their own, and the primary holder can manage anyone's
    /// (mirroring the canManagePremiumSeats-style holder check in ManageMembersViewModel).
    /// False for a non-holder editing someone else, which shouldn't normally be
    /// reachable anyway (only the holder's Manage Members can open another member's
    /// edit page), but this keeps the auth controls safe even if that ever changes.</summary>
    [ObservableProperty] public partial bool CanManageAuth { get; set; }
    [ObservableProperty] public partial DeviceAuthMode SelectedAuthMode { get; set; } = DeviceAuthMode.None;
    [ObservableProperty] public partial bool HasPin { get; set; }
    public ObservableCollection<DeviceAuthMode> AuthModeOptions { get; } = new(Enum.GetValues<DeviceAuthMode>());

    public MemberEditViewModel(IActiveSessionService session, IWorkoutRepository repo, ISeatAvailabilityService seats,
        IGoogleAuthService googleAuth, IFirebaseAuthService firebaseAuth, IIdentityService identity, IMemberPinService pins)
    {
        _session = session;
        _repo = repo;
        _seats = seats;
        _googleAuth = googleAuth;
        _firebaseAuth = firebaseAuth;
        _identity = identity;
        _pins = pins;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _isNew = (string)query["mode"] == "new";
        _memberId = query.TryGetValue("memberId", out var id) ? Guid.Parse((string)id) : null;
    }

    partial void OnSelectedRoleChanged(RolePreset value)
    {
        ApplyPresetToCheckboxes(value);
        UpdateCanLinkGoogle();
    }

    /// <summary>Age-gated: a Child never gets an independent external sign-in — only
    /// Teen/Adult/Guest/Owner may link their own Google account. Also requires the
    /// member to already exist (LinkGoogleAccount needs a real memberId to attach to).
    /// Recomputed whenever the role changes, so flipping a member from Child to Teen
    /// mid-edit reveals the option without needing to re-open the page.</summary>
    private void UpdateCanLinkGoogle() => CanLinkGoogle = !_isNew && SelectedRole != RolePreset.Child;

    /// <summary>Applies immediately (like LinkGoogleAccount/TogglePremium elsewhere in
    /// this app) rather than staging with the rest of the form and waiting for the
    /// page's Save button — a security setting shouldn't be ambiguous about whether
    /// cancelling the edit page also cancelled it. Guarded by _isLoadingAuthMode so
    /// populating the picker from LoadAsync doesn't itself trigger a save/prompt.</summary>
    partial void OnSelectedAuthModeChanged(DeviceAuthMode value)
    {
        if (_isLoadingAuthMode) return;
        _ = ApplyAuthModeChangeAsync(value);
    }

    private async Task ApplyAuthModeChangeAsync(DeviceAuthMode value)
    {
        if (_accountIndex is null || _memberId is not Guid memberId || !CanManageAuth) return;
        var member = _accountIndex.Members.FirstOrDefault(m => m.Id == memberId);
        if (member is null) return;

        switch (value)
        {
            case DeviceAuthMode.None:
                _pins.ClearPin(member);
                await _repo.SaveAccountIndexAsync(_accountIndex);
                HasPin = false;
                _lastAppliedAuthMode = DeviceAuthMode.None;
                break;

            case DeviceAuthMode.Biometric:
                member.DeviceAuthMode = DeviceAuthMode.Biometric;
                await _repo.SaveAccountIndexAsync(_accountIndex);
                _lastAppliedAuthMode = DeviceAuthMode.Biometric;
                break;

            case DeviceAuthMode.Pin:
                if (await PromptAndSetPinAsync(member))
                {
                    member.DeviceAuthMode = DeviceAuthMode.Pin;
                    await _repo.SaveAccountIndexAsync(_accountIndex);
                    HasPin = true;
                    _lastAppliedAuthMode = DeviceAuthMode.Pin;
                }
                else
                {
                    // Cancelled or mismatched — don't leave "Pin" selected with no
                    // PIN actually set, which would lock the profile with an
                    // unguessable, never-configured code.
                    _isLoadingAuthMode = true;
                    SelectedAuthMode = _lastAppliedAuthMode;
                    _isLoadingAuthMode = false;
                }
                break;
        }
    }

    private async Task<bool> PromptAndSetPinAsync(Member member)
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

    [RelayCommand]
    private async Task ChangePin()
    {
        if (_accountIndex is null || _memberId is not Guid memberId || !CanManageAuth) return;
        var member = _accountIndex.Members.FirstOrDefault(m => m.Id == memberId);
        if (member is null) return;
        if (await PromptAndSetPinAsync(member)) await _repo.SaveAccountIndexAsync(_accountIndex);
    }

    public async Task LoadAsync()
    {
        var account = _session.ActiveAccount;
        if (account is null) return;
        _accountIndex = await _repo.GetAccountIndexAsync(account.Id);
        if (_accountIndex is null) return;

        if (_isNew)
        {
            Title = "Add Member";
            ApplyPresetToCheckboxes(SelectedRole);
            return;
        }

        var member = _accountIndex.Members.FirstOrDefault(m => m.Id == _memberId);
        if (member is null) return;

        if (member.IdentityId is Guid identityId)
        {
            var identity = _accountIndex.Identities.FirstOrDefault(i => i.Id == identityId);
            IsLinked = identity is not null;
            LinkedGoogleLabel = identity?.Email ?? "Linked";
        }

        var viewer = _session.ActiveMember;
        CanManageAuth = viewer is not null && (viewer.Id == member.Id || viewer.Id == account.PrimaryHolderMemberId);
        _isLoadingAuthMode = true;
        SelectedAuthMode = member.DeviceAuthMode;
        _lastAppliedAuthMode = member.DeviceAuthMode;
        _isLoadingAuthMode = false;
        HasPin = !string.IsNullOrEmpty(member.PinHash);

        Title = $"Edit {member.DisplayName}";
        Name = member.DisplayName;
        SelectedColor = member.AvatarColor;
        if (member.DateOfBirth is DateOnly dob)
        {
            HasDateOfBirth = true;
            DateOfBirth = dob.ToDateTime(TimeOnly.MinValue);
        }
        // Not relying on OnSelectedRoleChanged firing here: the generated property
        // setter skips the partial method entirely when the new value equals the
        // current one (e.g. member.RolePreset is Adult, same as SelectedRole's
        // declared default) — confirmed by testing: editing an Adult member left
        // CanLinkGoogle false and the whole Google Account section missing, since
        // UpdateCanLinkGoogle() never ran. Call it explicitly instead.
        SelectedRole = member.RolePreset;
        UpdateCanLinkGoogle();

        var effective = member.EffectiveCapabilities();
        ViewOthersHistory = effective.ViewOthersHistory;
        CreateSharedWorkouts = effective.CreateSharedWorkouts;
        EditOthersWorkouts = effective.EditOthersWorkouts;
        EditOwnSchedule = effective.EditOwnSchedule;
        EditAnyoneSchedule = effective.EditAnyoneSchedule;
        DeleteContent = effective.DeleteContent;
        ManageMembers = effective.ManageMembers;
        ManageBilling = effective.ManageBilling;
        ExportData = effective.ExportData;
    }

    private void ApplyPresetToCheckboxes(RolePreset preset)
    {
        var caps = MemberCapabilities.ForPreset(preset);
        ViewOthersHistory = caps.ViewOthersHistory;
        CreateSharedWorkouts = caps.CreateSharedWorkouts;
        EditOthersWorkouts = caps.EditOthersWorkouts;
        EditOwnSchedule = caps.EditOwnSchedule;
        EditAnyoneSchedule = caps.EditAnyoneSchedule;
        DeleteContent = caps.DeleteContent;
        ManageMembers = caps.ManageMembers;
        ManageBilling = caps.ManageBilling;
        ExportData = caps.ExportData;
    }

    private MemberCapabilityOverrides? BuildOverrides()
    {
        var preset = MemberCapabilities.ForPreset(SelectedRole);
        var overrides = new MemberCapabilityOverrides
        {
            ViewOthersHistory = ViewOthersHistory != preset.ViewOthersHistory ? ViewOthersHistory : null,
            CreateSharedWorkouts = CreateSharedWorkouts != preset.CreateSharedWorkouts ? CreateSharedWorkouts : null,
            EditOthersWorkouts = EditOthersWorkouts != preset.EditOthersWorkouts ? EditOthersWorkouts : null,
            EditOwnSchedule = EditOwnSchedule != preset.EditOwnSchedule ? EditOwnSchedule : null,
            EditAnyoneSchedule = EditAnyoneSchedule != preset.EditAnyoneSchedule ? EditAnyoneSchedule : null,
            DeleteContent = DeleteContent != preset.DeleteContent ? DeleteContent : null,
            ManageMembers = ManageMembers != preset.ManageMembers ? ManageMembers : null,
            ManageBilling = ManageBilling != preset.ManageBilling ? ManageBilling : null,
            ExportData = ExportData != preset.ExportData ? ExportData : null,
        };
        var hasAny = overrides.ViewOthersHistory is not null || overrides.CreateSharedWorkouts is not null
            || overrides.EditOthersWorkouts is not null || overrides.EditOwnSchedule is not null
            || overrides.EditAnyoneSchedule is not null || overrides.DeleteContent is not null
            || overrides.ManageMembers is not null || overrides.ManageBilling is not null
            || overrides.ExportData is not null;
        return hasAny ? overrides : null;
    }

    [RelayCommand]
    private async Task Save()
    {
        if (_accountIndex is null || string.IsNullOrWhiteSpace(Name)) return;

        if (_isNew)
        {
            if (!_seats.HasCapacity(_accountIndex))
            {
                await Shell.Current.CurrentPage.DisplayAlertAsync("Seat limit reached",
                    "This account is at its seat limit. Remove a member or upgrade the plan first.", "OK");
                return;
            }

            var newMember = new Member
            {
                Id = Guid.NewGuid(),
                AccountId = _accountIndex.Account.Id,
                DisplayName = Name.Trim(),
                AvatarColor = SelectedColor,
                Initial = Name.Trim()[..1].ToUpperInvariant(),
                DateOfBirth = HasDateOfBirth ? DateOnly.FromDateTime(DateOfBirth) : null,
                RolePreset = SelectedRole,
                Overrides = BuildOverrides(),
                CreatedAt = DateTimeOffset.UtcNow,
            };
            _accountIndex.Members.Add(newMember);
            await _repo.SaveAccountIndexAsync(_accountIndex);
            await _repo.SaveMemberDataAsync(_accountIndex.Account.Id, newMember.Id, new MemberData());
        }
        else
        {
            var member = _accountIndex.Members.FirstOrDefault(m => m.Id == _memberId);
            if (member is null) return;
            member.DisplayName = Name.Trim();
            member.AvatarColor = SelectedColor;
            member.Initial = Name.Trim()[..1].ToUpperInvariant();
            member.DateOfBirth = HasDateOfBirth ? DateOnly.FromDateTime(DateOfBirth) : null;
            member.RolePreset = SelectedRole;
            member.Overrides = BuildOverrides();
            await _repo.SaveAccountIndexAsync(_accountIndex);
        }

        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task Cancel() => await Shell.Current.GoToAsync("..");

    /// <summary>
    /// Runs the real Google sign-in flow, exchanges it for a Firebase session (the
    /// same one ProfileGateViewModel's sign-in uses — see CompleteSignInAsync there),
    /// and on success ties this member to the resulting Identity — creating one if
    /// this Google account has never been linked in this account before, reusing it
    /// if it has. Also claims this account under that identity server-side
    /// (/identities/claim) so signing in with this same Google account on a new
    /// device finds this account, not just a local label.
    /// </summary>
    [RelayCommand]
    private async Task LinkGoogleAccount()
    {
        if (_accountIndex is null || _memberId is not Guid memberId || IsLinking) return;
        var page = Shell.Current?.CurrentPage;

        IsLinking = true;
        try
        {
            var googleResult = await _googleAuth.SignInAsync();
            if (googleResult is null)
            {
                if (page is not null) await page.DisplayAlertAsync("Sign-in cancelled", "No changes made.", "OK");
                return;
            }

            var firebaseResult = await _firebaseAuth.SignInWithGoogleAsync(googleResult.IdToken);
            if (firebaseResult is null)
            {
                if (page is not null) await page.DisplayAlertAsync("Sign-in failed", _firebaseAuth.LastError ?? "Unknown error.", "OK");
                return;
            }

            var identity = _accountIndex.Identities.FirstOrDefault(i => i.AuthProviderRef == firebaseResult.Uid);
            if (identity is null)
            {
                identity = new Identity { Id = Guid.NewGuid(), AuthProviderRef = firebaseResult.Uid, CreatedAt = DateTimeOffset.UtcNow };
                _accountIndex.Identities.Add(identity);
            }
            identity.Email = firebaseResult.Email;

            var member = _accountIndex.Members.FirstOrDefault(m => m.Id == memberId);
            if (member is null) return;
            member.IdentityId = identity.Id;
            await _repo.SaveAccountIndexAsync(_accountIndex);

            try { await _identity.ClaimAsync(firebaseResult.IdToken, _accountIndex.Account.Id); }
            catch { /* the local link still works; it just won't be found from another device until a retry succeeds */ }

            IsLinked = true;
            LinkedGoogleLabel = identity.Email ?? "Linked";
        }
        finally
        {
            IsLinking = false;
        }
    }

    [RelayCommand]
    private async Task UnlinkGoogleAccount()
    {
        if (_accountIndex is null || _memberId is not Guid memberId) return;
        var member = _accountIndex.Members.FirstOrDefault(m => m.Id == memberId);
        if (member is null) return;

        member.IdentityId = null;
        await _repo.SaveAccountIndexAsync(_accountIndex);
        IsLinked = false;
        LinkedGoogleLabel = "Not linked";
    }
}
