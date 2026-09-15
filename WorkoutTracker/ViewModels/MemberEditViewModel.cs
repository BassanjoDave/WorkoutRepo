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
    private readonly IMemberAuthGateService _authGate;
    private readonly RemoteApiWorkoutRepository _remoteApi;
    private readonly IProgressPhotoCaptureService _photoCapture;

    private bool _isNew;
    private Guid? _memberId;
    private AccountIndex? _accountIndex;
    private bool _isLoadingAuthMode;
    private DeviceAuthMode _lastAppliedAuthMode = DeviceAuthMode.None;

    /// <summary>The blob filename already saved on the member being edited, if any — used to know what to delete when a new photo replaces it or Remove Photo is used.</summary>
    private string? _existingPhotoBlobFileName;
    /// <summary>A newly captured photo not yet persisted — Save() writes it to the blob store and only then assigns Member.AvatarPhotoBlobFileName.</summary>
    private byte[]? _pendingPhotoBytes;
    private bool _photoRemoved;

    /// <summary>Holds a not-yet-saved member's PIN/DeviceAuthMode while _isNew — there's
    /// no row in _accountIndex.Members to attach it to until Save() succeeds, but the
    /// PROFILE PROTECTION card needs somewhere to set/verify a PIN before that point
    /// (a new member can no longer be saved without one — see Save()). Never added to
    /// _accountIndex.Members itself; Save() copies its DeviceAuthMode/PinHash onto the
    /// real Member it constructs.</summary>
    private Member? _draftMember;

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

    [ObservableProperty] public partial bool HasPhoto { get; set; }
    [ObservableProperty] public partial bool UsePhotoAvatar { get; set; }
    [ObservableProperty] public partial ImageSource? PhotoPreview { get; set; }
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

    /// <summary>The LINKED SIGN-IN card should show whenever there's something to
    /// show OR do — either a member already has any identity linked (Google or a
    /// provisioned credential; CanLinkGoogle alone would hide this for a Child, who
    /// can never satisfy CanLinkGoogle's age gate but absolutely can have a
    /// provisioned credential), or Google-linking is actually offered. The "Link
    /// Google Account" button itself stays gated on CanLinkGoogle specifically, so a
    /// Child never sees that particular action even while this card is visible for
    /// their provisioned credential.</summary>
    public bool ShowLinkedIdentityCard => IsLinked || CanLinkGoogle;
    public bool ShowLinkGoogleButton => CanLinkGoogle && !IsLinked;
    partial void OnIsLinkedChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowLinkedIdentityCard));
        OnPropertyChanged(nameof(ShowLinkGoogleButton));
    }
    partial void OnCanLinkGoogleChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowLinkedIdentityCard));
        OnPropertyChanged(nameof(ShowLinkGoogleButton));
    }

    /// <summary>Whether the INDEPENDENT SIGN-IN card's "Set up sign-in" action should
    /// show — unlike CanLinkGoogle, deliberately NOT role-restricted: this is the one
    /// path that actually lets a Child sign in on their own device (Google linking
    /// requires the member to interactively OAuth as themselves, which a Child can't/
    /// shouldn't do; a holder typing an email+password for them has no such
    /// constraint). Still requires a real, saved memberId (same !_isNew reasoning as
    /// CanLinkGoogle) and hides once any identity (Google or credential) is already
    /// linked, since Member.IdentityId only ever holds one at a time.</summary>
    [ObservableProperty] public partial bool CanProvisionCredential { get; set; }
    [ObservableProperty] public partial bool IsProvisioning { get; set; }

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
        IGoogleAuthService googleAuth, IFirebaseAuthService firebaseAuth, IIdentityService identity, IMemberPinService pins,
        IMemberAuthGateService authGate, RemoteApiWorkoutRepository remoteApi, IProgressPhotoCaptureService photoCapture)
    {
        _session = session;
        _repo = repo;
        _seats = seats;
        _googleAuth = googleAuth;
        _firebaseAuth = firebaseAuth;
        _identity = identity;
        _pins = pins;
        _authGate = authGate;
        _remoteApi = remoteApi;
        _photoCapture = photoCapture;
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

    /// <summary>The member a PIN/DeviceAuthMode change should apply to — the real,
    /// already-saved Member while editing, or the in-memory _draftMember while
    /// _isNew (there's no row in _accountIndex.Members yet). See _draftMember's
    /// doc comment for why this can't just be "look it up by _memberId" during
    /// creation.</summary>
    private Member? GetEditingMember()
    {
        if (!_isNew) return _accountIndex?.Members.FirstOrDefault(m => m.Id == _memberId);
        if (_draftMember is not null) _draftMember.DisplayName = string.IsNullOrWhiteSpace(Name) ? "this member" : Name.Trim();
        return _draftMember;
    }

    private async Task ApplyAuthModeChangeAsync(DeviceAuthMode value)
    {
        if (_accountIndex is null || !CanManageAuth) return;
        var member = GetEditingMember();
        if (member is null) return;

        switch (value)
        {
            case DeviceAuthMode.None:
                _pins.ClearPin(member);
                if (!_isNew) await _repo.SaveAccountIndexAsync(_accountIndex);
                HasPin = false;
                _lastAppliedAuthMode = DeviceAuthMode.None;
                break;

            case DeviceAuthMode.Biometric:
                member.DeviceAuthMode = DeviceAuthMode.Biometric;
                if (!_isNew) await _repo.SaveAccountIndexAsync(_accountIndex);
                _lastAppliedAuthMode = DeviceAuthMode.Biometric;
                break;

            case DeviceAuthMode.Pin:
                if (await _authGate.PromptAndSetPinAsync(member))
                {
                    member.DeviceAuthMode = DeviceAuthMode.Pin;
                    if (!_isNew) await _repo.SaveAccountIndexAsync(_accountIndex);
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

    [RelayCommand]
    private async Task ChangePin()
    {
        if (_accountIndex is null || !CanManageAuth) return;
        var member = GetEditingMember();
        if (member is null) return;
        if (await _authGate.PromptAndSetPinAsync(member) && !_isNew) await _repo.SaveAccountIndexAsync(_accountIndex);
    }

    /// <summary>Captures a photo but doesn't persist it — Save() writes the blob and assigns it to the Member, mirroring every other field on this page staying staged until Save.</summary>
    [RelayCommand]
    private async Task AddPhoto()
    {
        var bytes = await _photoCapture.CaptureAsync("Add Profile Photo");
        if (bytes is null) return;

        _pendingPhotoBytes = bytes;
        _photoRemoved = false;
        PhotoPreview = ImageSource.FromStream(_ => Task.FromResult<Stream>(new MemoryStream(bytes)));
        HasPhoto = true;
        UsePhotoAvatar = true;
    }

    [RelayCommand]
    private void RemovePhoto()
    {
        _pendingPhotoBytes = null;
        _photoRemoved = true;
        PhotoPreview = null;
        HasPhoto = false;
        UsePhotoAvatar = false;
    }

    /// <summary>
    /// Applies whatever photo action was staged (capture, removal, or just a
    /// preference flip) to the given Member — shared by both Save() branches
    /// since a photo can be added while creating a new member too. Must run
    /// before SaveAccountIndexAsync since it sets AvatarPhotoBlobFileName/
    /// AvatarDisplay on the Member that call persists.
    /// </summary>
    private async Task ApplyPhotoAsync(Member member, Guid accountId)
    {
        if (_pendingPhotoBytes is byte[] bytes)
        {
            var newBlobFileName = $"{Guid.NewGuid()}.jpg";
            await _repo.SaveProgressPhotoBlobAsync(accountId, member.Id, newBlobFileName, bytes);
            if (_existingPhotoBlobFileName is string oldBlobFileName)
            {
                try { await _repo.DeleteProgressPhotoBlobAsync(accountId, member.Id, oldBlobFileName); }
                catch { /* best-effort cleanup; an orphaned blob costs storage, not correctness */ }
            }
            member.AvatarPhotoBlobFileName = newBlobFileName;
            member.AvatarDisplay = AvatarDisplay.Photo;
        }
        else if (_photoRemoved && _existingPhotoBlobFileName is string blobFileName)
        {
            try { await _repo.DeleteProgressPhotoBlobAsync(accountId, member.Id, blobFileName); }
            catch { /* best-effort cleanup */ }
            member.AvatarPhotoBlobFileName = null;
            member.AvatarDisplay = AvatarDisplay.Initial;
        }
        else
        {
            member.AvatarDisplay = UsePhotoAvatar && member.AvatarPhotoBlobFileName is not null ? AvatarDisplay.Photo : AvatarDisplay.Initial;
        }
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
            // A brand-new member can't be saved without a PIN (see Save()), so the
            // PROFILE PROTECTION card needs to be reachable during creation too —
            // previously CanManageAuth only ever became true for an existing member.
            CanManageAuth = true;
            _draftMember = new Member { Id = Guid.NewGuid(), AccountId = account.Id };
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
        CanProvisionCredential = !IsLinked;

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

        _existingPhotoBlobFileName = member.AvatarPhotoBlobFileName;
        HasPhoto = _existingPhotoBlobFileName is not null;
        UsePhotoAvatar = member.AvatarDisplay == AvatarDisplay.Photo;
        if (_existingPhotoBlobFileName is string blobFileName)
        {
            var photoAccountId = account.Id;
            var photoMemberId = member.Id;
            PhotoPreview = ImageSource.FromStream(async _ =>
            {
                var bytes = await _repo.GetProgressPhotoBlobAsync(photoAccountId, photoMemberId, blobFileName);
                return bytes is null ? null : new MemoryStream(bytes);
            });
        }

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

        // Every member needs a PIN/biometric before they're usable — otherwise
        // anyone with physical access to an already-unlocked device can switch
        // into them (or, for an admin-capable member, be switched OUT of into
        // them) with zero verification. See MemberCapabilities.RequiresMandatoryGate
        // for the stricter case this also finally makes meaningful: an admin-tier
        // member without a PIN was previously indistinguishable from a harmless one.
        var pendingAuthMode = _isNew ? _draftMember?.DeviceAuthMode ?? DeviceAuthMode.None : SelectedAuthMode;
        if (pendingAuthMode == DeviceAuthMode.None)
        {
            // Mirrors MemberCapabilities.RequiresMandatoryGate against the live toggle
            // values rather than the persisted/preset ones, since the whole point is to
            // catch a toggle the user just flipped on but hasn't saved yet.
            var requiresGate = ManageMembers || ManageBilling || DeleteContent;
            var message = requiresGate
                ? "This member can manage members, manage billing, or delete others' content — a PIN is required before it can be granted."
                : "Every profile needs a PIN so restrictions on a shared device actually hold. Set one under PROFILE PROTECTION before saving.";
            await Shell.Current.CurrentPage.DisplayAlertAsync("PIN required", message, "OK");
            return;
        }

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
                Id = _draftMember?.Id ?? Guid.NewGuid(),
                AccountId = _accountIndex.Account.Id,
                DisplayName = Name.Trim(),
                AvatarColor = SelectedColor,
                Initial = Name.Trim()[..1].ToUpperInvariant(),
                DateOfBirth = HasDateOfBirth ? DateOnly.FromDateTime(DateOfBirth) : null,
                RolePreset = SelectedRole,
                Overrides = BuildOverrides(),
                CreatedAt = DateTimeOffset.UtcNow,
                DeviceAuthMode = _draftMember?.DeviceAuthMode ?? DeviceAuthMode.None,
                PinHash = _draftMember?.PinHash,
            };
            await ApplyPhotoAsync(newMember, _accountIndex.Account.Id);
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
            await ApplyPhotoAsync(member, _accountIndex.Account.Id);
            await _repo.SaveAccountIndexAsync(_accountIndex);

            // Refresh IActiveSessionService's cached ActiveMember when it's the member
            // just edited — otherwise Home/Profile keep rendering the pre-edit Initial/
            // AvatarDisplay/photo for the rest of the app session, since ActiveMember is
            // a separate in-memory reference from the AccountIndex fetched above. Mirrors
            // DevSettingsViewModel.SaveEntitlements()'s identical re-select-after-save fix
            // for the same underlying staleness (confirmed by testing there).
            if (_session.ActiveMember?.Id == member.Id)
            {
                await _session.SelectMemberAsync(_accountIndex.Account.Id, member.Id);
            }
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
            CanProvisionCredential = false;
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
        CanProvisionCredential = true;
    }

    /// <summary>Sets up a brand-new, independent email+password sign-in for this
    /// member (see WorkoutTracker.Api Program.cs's POST .../credential) — the path
    /// that actually lets a dependent (including a Child, unlike LinkGoogleAccount)
    /// sign in on their own separate device and land straight in their own profile.
    /// The holder enters the credential directly here rather than the member setting
    /// it themselves, since this screen is only reachable by the holder or the member
    /// being edited, and a Child can't run an invite/reset-email flow independently.</summary>
    [RelayCommand]
    private async Task ProvisionCredential()
    {
        if (_accountIndex is null || _memberId is not Guid memberId || IsProvisioning) return;
        var page = Shell.Current?.CurrentPage;
        if (page is null) return;

        var email = await page.DisplayPromptAsync("Independent sign-in", $"Email address for {Name}", keyboard: Keyboard.Email);
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            if (!string.IsNullOrWhiteSpace(email)) await page.DisplayAlertAsync("Invalid email", "Enter a valid email address.", "OK");
            return;
        }

        var password = await page.DisplayPromptAsync("Independent sign-in", $"Choose a password for {Name} (6+ characters)");
        if (string.IsNullOrWhiteSpace(password)) return; // cancelled
        if (password.Length < 6)
        {
            await page.DisplayAlertAsync("Password too short", "Password must be at least 6 characters.", "OK");
            return;
        }

        string uid;
        IsProvisioning = true;
        try
        {
            uid = await _remoteApi.ProvisionDependentCredentialAsync(_accountIndex.Account.Id, memberId, email.Trim(), password);
        }
        catch (Exception ex)
        {
            await page.DisplayAlertAsync("Couldn't set up sign-in", ex.Message, "OK");
            return;
        }
        finally
        {
            IsProvisioning = false;
        }

        // Build the local Identity with the real Uid the server just returned —
        // same pattern as LinkGoogleAccount just below, which also has the real Uid
        // in hand before ever touching _accountIndex. This is safe to push through
        // the normal SaveAccountIndexAsync/sync-outbox path since it's already
        // correct, unlike a re-fetch through IWorkoutRepository (which may be a
        // local-first cache that hasn't observed this out-of-band server write yet).
        var identity = new Identity { Id = Guid.NewGuid(), Email = email.Trim(), AuthProviderRef = uid, CreatedAt = DateTimeOffset.UtcNow };
        _accountIndex.Identities.Add(identity);
        var member = _accountIndex.Members.FirstOrDefault(m => m.Id == memberId);
        if (member is not null) member.IdentityId = identity.Id;
        await _repo.SaveAccountIndexAsync(_accountIndex);
        await NotificationWriter.NotifyMemberAsync(_repo, _accountIndex.Account.Id, memberId,
            "Your independent sign-in is ready",
            "You can now sign in to this family account on your own device.");

        IsLinked = true;
        LinkedGoogleLabel = email.Trim();
        CanProvisionCredential = false;
    }
}
