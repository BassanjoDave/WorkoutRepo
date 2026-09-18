using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.ViewModels;

public partial class ProfileGateViewModel : ObservableObject
{
    private readonly IAppBootstrapper _bootstrapper;
    private readonly IWorkoutRepository _repo;
    private readonly IActiveSessionService _session;
    private readonly IGoogleAuthService _googleAuth;
    private readonly IFirebaseAuthService _firebaseAuth;
    private readonly IIdentityService _identity;
    private readonly IBiometricAuthService _biometrics;
    private readonly IKeyboardService _keyboard;
    private readonly IMemberAuthGateService _authGate;

    /// <summary>Preferences key shared with ProfileViewModel's own toggle for the
    /// same setting — plain Preferences (not SecureStorage) since it's just an
    /// on/off flag, not a secret; the actual session it unlocks is what's sensitive,
    /// and that's already in SecureStorage via IFirebaseAuthService.</summary>
    public const string BiometricUnlockPrefKey = "biometric_unlock_enabled";

    // The Base ramp's Avatar1-6 colors (Resources/Styles/Colors.Base.xaml) — mirrored
    // here since a brand-new member created from sign-in needs one before any page's
    // resources are available to pick from.
    private static readonly string[] AvatarColors = { "#8085DC", "#00A6AE", "#C36DA8", "#A29015", "#D26E5E", "#31A773" };

    [ObservableProperty]
    public partial ObservableCollection<MemberTileViewModel> Members { get; set; } = new();

    [ObservableProperty]
    public partial bool IsLoading { get; set; } = true;

    [ObservableProperty]
    public partial bool IsSigningIn { get; set; }

    /// <summary>False until a sign-in succeeds this launch — the tile list of
    /// already-known local accounts only appears afterward, never instead of
    /// signing in. See ProfileGatePage.xaml's IsVisible bindings.</summary>
    [ObservableProperty]
    public partial bool IsUnlocked { get; set; }

    /// <summary>True when this screen was reached via "Switch" from an already
    /// active session (not a fresh app launch) — lets the user back out to Home
    /// instead of being stuck here with no session to fall back to.</summary>
    [ObservableProperty]
    public partial bool CanCancel { get; set; }

    [ObservableProperty]
    public partial string Email { get; set; } = "";

    [ObservableProperty]
    public partial string Password { get; set; } = "";

    [ObservableProperty]
    public partial bool PasswordHidden { get; set; } = true;

    [RelayCommand]
    private void ToggleShowPassword() => PasswordHidden = !PasswordHidden;

    [RelayCommand]
    private async Task OpenTerms() => await Shell.Current.GoToAsync("legal?doc=terms");

    [RelayCommand]
    private async Task OpenPrivacy() => await Shell.Current.GoToAsync("legal?doc=privacy");

    /// <summary>Shown at the top of a fresh-launch gate only when biometric unlock is
    /// turned on (Profile settings), the device actually has working biometric
    /// hardware right now, and there's a still-resumable stored session to unlock —
    /// never shown in switch mode (CanCancel), which already has its own explicit
    /// sign-in-with-a-different-account intent.</summary>
    [ObservableProperty]
    public partial bool ShowBiometricUnlock { get; set; }

    /// <summary>The sign-in form (Google/email/create-account) shows once loading has
    /// finished and until a sign-in actually succeeds on a fresh launch — but stays
    /// available alongside the tile list in switch mode (CanCancel), since "Switch"
    /// should let you sign in with a different account too, not just pick among the
    /// ones already on this device.</summary>
    public bool ShowSignIn => !IsLoading && (!IsUnlocked || CanCancel);

    /// <summary>The plain "Sign in to continue" heading — only for a fresh-launch
    /// gate, not switch mode (which gets its own heading below).</summary>
    public bool ShowSignInHeading => ShowSignIn && !CanCancel;

    /// <summary>The "Who's working out?" heading — only for a fresh-launch gate that
    /// already succeeded, not switch mode (which gets its own heading below).</summary>
    public bool ShowUnlockedHeading => IsUnlocked && !CanCancel;

    partial void OnIsLoadingChanged(bool value) => RaiseHeadingChanges();
    partial void OnIsUnlockedChanged(bool value) => RaiseHeadingChanges();
    partial void OnCanCancelChanged(bool value) => RaiseHeadingChanges();

    private void RaiseHeadingChanges()
    {
        OnPropertyChanged(nameof(ShowSignIn));
        OnPropertyChanged(nameof(ShowSignInHeading));
        OnPropertyChanged(nameof(ShowUnlockedHeading));
    }

    public ProfileGateViewModel(IAppBootstrapper bootstrapper, IWorkoutRepository repo, IActiveSessionService session,
        IGoogleAuthService googleAuth, IFirebaseAuthService firebaseAuth, IIdentityService identity, IBiometricAuthService biometrics,
        IKeyboardService keyboard, IMemberAuthGateService authGate)
    {
        _bootstrapper = bootstrapper;
        _repo = repo;
        _session = session;
        _googleAuth = googleAuth;
        _firebaseAuth = firebaseAuth;
        _identity = identity;
        _biometrics = biometrics;
        _keyboard = keyboard;
        _authGate = authGate;
    }

    private const string OnboardingShownKey = "onboarding_shown";

    public async Task LoadAsync()
    {
        // First-ever launch on this device — a one-time detour to a marketing/
        // feature-tour carousel before the sign-in gate itself. OnboardingPage pops
        // straight back here (GoToAsync("..")), and since ProfileGatePage.OnAppearing
        // calls LoadAsync every time it appears, the rest of this method then runs
        // normally on the very next appearance, with the flag already set.
        if (!Preferences.Default.Get(OnboardingShownKey, false))
        {
            Preferences.Default.Set(OnboardingShownKey, true);
            await Shell.Current.GoToAsync("onboarding");
            return;
        }

        await CheckForLastCrashAsync();

        IsLoading = true;
        IsUnlocked = false;
        try
        {
            await _bootstrapper.EnsureSeedDataAsync();
        }
        catch
        {
            // Seed-data content is a nice-to-have, not a login gate — a bad
            // seeding step must never leave this, the app's very first screen,
            // stuck on a spinner forever with no member list and no way out.
        }

        // Reached via "Switch" from an already-signed-in session this launch
        // (ActiveMember only gets set by SelectMemberAsync — never restored on cold
        // start, see ActiveSessionService) — no need to re-authenticate, just let
        // them pick a different already-known profile, with a way back out.
        if (_session.ActiveMember is not null)
        {
            CanCancel = true;
            await RefreshMembersAsync();
            IsUnlocked = true;
        }
        else
        {
            CanCancel = false;

            // Checking this also transparently refreshes and caches a valid ID token
            // when one's available (see GetValidIdTokenAsync) — a small bit of early
            // work now that makes UnlockWithBiometrics feel instant once tapped,
            // rather than wasted effort, since we needed the answer either way.
            ShowBiometricUnlock = Preferences.Default.Get(BiometricUnlockPrefKey, false)
                && _biometrics.IsSupported
                && await _biometrics.IsAvailableAsync()
                && await _firebaseAuth.ResumeSessionAsync() is not null;
        }

        IsLoading = false;
    }

    [RelayCommand]
    private async Task Cancel() => await Shell.Current.GoToAsync("//home");

    [RelayCommand]
    private async Task UnlockWithBiometrics()
    {
        if (IsSigningIn) return;
        IsSigningIn = true;
        var page = Shell.Current?.CurrentPage;
        try
        {
            var confirmed = await _biometrics.AuthenticateAsync("Sign in to Rig Ritual");
            if (!confirmed) return; // cancelled or failed the scan — the normal sign-in form is still right there

            var result = await _firebaseAuth.ResumeSessionAsync();
            if (result is null)
            {
                // The stored session died between the button appearing and now
                // (revoked elsewhere, expired refresh token) — fall back cleanly.
                ShowBiometricUnlock = false;
                if (page is not null) await page.DisplayAlertAsync("Session expired", "Please sign in again.", "OK");
                return;
            }
            await CompleteSignInAsync(result, null);
        }
        finally
        {
            IsSigningIn = false;
        }
    }

    /// <summary>Resolves a signed-in Firebase Uid to exactly one specific member's
    /// own account+member id, across every account this identity is linked to —
    /// returns null on zero or more than one match (ambiguous, so the caller should
    /// fall back to showing the tile picker rather than guessing).</summary>
    private async Task<(Guid AccountId, Guid MemberId)?> FindOwnMemberAsync(List<Guid> accountIds, string uid)
    {
        (Guid, Guid)? found = null;
        foreach (var accountId in accountIds)
        {
            var accountIndex = await _repo.GetAccountIndexAsync(accountId);
            if (accountIndex is null) continue;

            var identity = accountIndex.Identities.FirstOrDefault(i => i.AuthProviderRef == uid);
            if (identity is null) continue;

            foreach (var member in accountIndex.Members.Where(m => m.Status == MemberStatus.Active && m.IdentityId == identity.Id))
            {
                if (found is not null) return null; // ambiguous — more than one match
                found = (accountId, member.Id);
            }
        }
        return found;
    }

    private async Task RefreshMembersAsync()
    {
        var deviceIndex = await _repo.GetDeviceIndexAsync();
        // Replacing the whole collection (rather than Clear() + Add() in place) avoids
        // BindableLayout briefly seeing an empty source mid-rebuild — that transient
        // empty state crashes natively inside WinUI's own child-collection handling
        // (see the same fix in WorkoutsViewModel.Rebuild()).
        var members = new ObservableCollection<MemberTileViewModel>();
        foreach (var accountId in deviceIndex.AccountIds)
        {
            var accountIndex = await _repo.GetAccountIndexAsync(accountId);
            if (accountIndex is null) continue;
            foreach (var member in accountIndex.Members.Where(m => m.Status == MemberStatus.Active))
            {
                members.Add(new MemberTileViewModel(accountId, member, SelectMemberCommand, _repo));
            }
        }
        Members = members;
    }

    /// <summary>
    /// Surfaces the previous run's crash (if any) so it can actually be
    /// reported without device debugging access — copies the full text to
    /// the clipboard and shows a short on-screen preview.
    /// </summary>
    private async Task CheckForLastCrashAsync()
    {
        var crash = CrashLogger.TakeLastCrash();
        if (crash is null) return;

        try { await Clipboard.Default.SetTextAsync(crash); }
        catch { /* clipboard unavailable — the alert still shows a preview below */ }

        var preview = crash.Length > 500 ? crash[..500] + "…" : crash;
        var page = Shell.Current?.CurrentPage;
        if (page is not null)
        {
            await page.DisplayAlertAsync("The app crashed last time",
                "Full details were copied to your clipboard — paste them to report it.\n\n" + preview, "OK");
        }
    }

    [RelayCommand]
    private async Task SelectMember(MemberTileViewModel? tile)
    {
        if (tile is null) return;

        // Re-fetch the real Member (not just the tile DTO, which deliberately
        // doesn't carry PinHash) to actually verify against — see
        // IMemberAuthGateService's doc comment for why this check exists.
        // Unconditional now (not just when tile.DeviceAuthMode != None): every
        // member is required to have a PIN, so VerifyOrEstablishAsync forces setup
        // on the spot for a member who somehow still doesn't (a pre-fix account
        // that hasn't been prompted yet) instead of silently allowing the switch.
        var accountIndex = await _repo.GetAccountIndexAsync(tile.AccountId);
        var member = accountIndex?.Members.FirstOrDefault(m => m.Id == tile.MemberId);
        if (accountIndex is null || member is null) return;
        if (!await _authGate.VerifyOrEstablishAsync(member, accountIndex, _repo, $"Switch to {tile.Name}")) return;

        var result = await _session.SelectMemberAsync(tile.AccountId, tile.MemberId);
        if (result is not null)
        {
            await Shell.Current.GoToAsync("//home");
        }
    }

    /// <summary>
    /// Sign in with Google — gets a Google ID token via IGoogleAuthService's
    /// per-platform OAuth flow (unchanged), then exchanges it for a Firebase session
    /// via IFirebaseAuthService so it's verified/looked-up the exact same way as
    /// every other sign-in method (see CompleteSignInAsync). Separate from
    /// MemberEditViewModel.LinkGoogleAccount, which links a Google identity to an
    /// already-existing local member instead of establishing app entry.
    /// </summary>
    [RelayCommand]
    private async Task SignInWithGoogle()
    {
        if (IsSigningIn) return;
        IsSigningIn = true;
        var page = Shell.Current?.CurrentPage;
        try
        {
            var googleResult = await _googleAuth.SignInAsync();
            if (googleResult is null)
            {
                // LastError is null when the user simply cancelled — nothing to show for that.
                if (_googleAuth.LastError is string authError && page is not null)
                {
                    await page.DisplayAlertAsync("Sign-in failed", authError, "OK");
                }
                return;
            }

            var firebaseResult = await _firebaseAuth.SignInWithGoogleAsync(googleResult.IdToken);
            if (firebaseResult is null)
            {
                if (page is not null) await page.DisplayAlertAsync("Sign-in failed", _firebaseAuth.LastError ?? "Unknown error.", "OK");
                return;
            }

            await CompleteSignInAsync(firebaseResult, googleResult.Name);
        }
        finally
        {
            IsSigningIn = false;
        }
    }

    [RelayCommand]
    private async Task SignInWithEmail()
    {
        if (IsSigningIn || !ValidateEmailPassword(out var page)) return;
        IsSigningIn = true;
        try
        {
            var result = await _firebaseAuth.SignInWithEmailAsync(Email.Trim(), Password);
            if (result is null)
            {
                if (page is not null) await page.DisplayAlertAsync("Sign-in failed", _firebaseAuth.LastError ?? "Unknown error.", "OK");
                return;
            }
            await CompleteSignInAsync(result, null);
        }
        finally
        {
            IsSigningIn = false;
        }
    }

    [RelayCommand]
    private async Task SignUpWithEmail()
    {
        if (IsSigningIn || !ValidateEmailPassword(out var page)) return;
        IsSigningIn = true;
        try
        {
            var result = await _firebaseAuth.SignUpWithEmailAsync(Email.Trim(), Password);
            if (result is null)
            {
                if (page is not null) await page.DisplayAlertAsync("Couldn't create account", _firebaseAuth.LastError ?? "Unknown error.", "OK");
                return;
            }
            // Best-effort — a failed send here shouldn't block account creation;
            // MemberEditPage/Profile can offer "resend" later if this silently fails.
            try { await _firebaseAuth.SendEmailVerificationAsync(result.IdToken); } catch { /* non-fatal */ }
            await CompleteSignInAsync(result, null);
        }
        finally
        {
            IsSigningIn = false;
        }
    }

    /// <summary>Asks Firebase to email a password-reset link for whatever's currently
    /// typed in the Email field — reuses that field rather than a separate dialog,
    /// since it's already right there above the password box on this same form.</summary>
    [RelayCommand]
    private async Task ForgotPassword()
    {
        var page = Shell.Current?.CurrentPage;
        if (page is null) return;
        if (string.IsNullOrWhiteSpace(Email))
        {
            await page.DisplayAlertAsync("Enter your email", "Type your email above first, then tap \"Forgot password?\" again.", "OK");
            return;
        }

        IsSigningIn = true;
        try
        {
            await _firebaseAuth.SendPasswordResetAsync(Email.Trim());
        }
        finally
        {
            IsSigningIn = false;
        }
        // Always the same message regardless of whether that email is registered —
        // see IFirebaseAuthService.SendPasswordResetAsync's doc comment for why.
        await page.DisplayAlertAsync("Check your email",
            $"If an account exists for {Email.Trim()}, a password reset link has been sent.", "OK");
    }

    private bool ValidateEmailPassword(out Page? page)
    {
        page = Shell.Current?.CurrentPage;
        return !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Password);
    }

    /// <summary>
    /// Shared by every sign-in method once each has produced a verified Firebase
    /// session: asks the server-side identity directory whether this identity
    /// already has an account anywhere (/identities/lookup). If so, that account is
    /// pulled onto this device and unlocked into the tile list below. If this is the
    /// first time this identity has ever signed in, a brand-new real account is
    /// created for it and claimed under that identity (/identities/claim) so a
    /// future device can find it the same way.
    /// </summary>
    private async Task CompleteSignInAsync(FirebaseAuthResult result, string? googleDisplayName)
    {
        var page = Shell.Current?.CurrentPage;
        List<Guid> foundAccountIds;
        try
        {
            foundAccountIds = await _identity.LookupAsync(result.IdToken);
        }
        catch (Exception ex)
        {
            if (page is not null) await page.DisplayAlertAsync("Sign-in failed", $"Couldn't reach the server: {ex.Message}", "OK");
            return;
        }

        var deviceIndex = await _repo.GetDeviceIndexAsync();

        if (foundAccountIds.Count > 0)
        {
            foreach (var foundId in foundAccountIds.Where(id => !deviceIndex.AccountIds.Contains(id)))
            {
                deviceIndex.AccountIds.Add(foundId);
            }
            await _repo.SaveDeviceIndexAsync(deviceIndex);

            // If this identity is specifically ONE member's own linked sign-in (not
            // just a shared credential the account's primary holder types in for
            // everyone), skip the tile picker entirely and land on that member's own
            // Home — see MemberEditViewModel.LinkGoogleAccount, the only other place
            // an Identity gets tied to a specific Member.IdentityId. Falls back to
            // the normal picker below on zero or more than one match (e.g. the
            // primary holder's shared login, which isn't any single member's own
            // IdentityId).
            var ownedMember = await FindOwnMemberAsync(foundAccountIds, result.Uid);
            if (ownedMember is not null)
            {
                var (ownAccountId, ownMemberId) = ownedMember.Value;
                var ownSelected = await _session.SelectMemberAsync(ownAccountId, ownMemberId);
                if (ownSelected is not null)
                {
                    _keyboard.HideKeyboard();
                    await Shell.Current.GoToAsync("//home");
                    return;
                }
            }

            IsUnlocked = true;
            _keyboard.HideKeyboard();
            await RefreshMembersAsync();
            return;
        }

        // No account anywhere is linked to this identity yet — this is a genuinely
        // new sign-in, so create a real account for it.
        var now = DateTimeOffset.UtcNow;
        var accountId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var identityId = Guid.NewGuid();
        var displayName = googleDisplayName ?? result.DisplayName ?? result.Email ?? "Member";
        var initial = displayName.Length > 0 ? displayName[..1].ToUpperInvariant() : "?";

        var account = new Account
        {
            Id = accountId, PrimaryHolderMemberId = memberId,
            DisplayName = $"{displayName}'s Account", CreatedAt = now,
        };
        var member = new Member
        {
            Id = memberId, AccountId = accountId, DisplayName = displayName,
            AvatarColor = AvatarColors[Random.Shared.Next(AvatarColors.Length)], Initial = initial,
            RolePreset = RolePreset.Owner, CreatedAt = now, IdentityId = identityId,
        };
        var identity = new Identity { Id = identityId, Email = result.Email, AuthProviderRef = result.Uid, CreatedAt = now };

        // Every member needs a PIN (see MemberEditViewModel.Save()), including a
        // brand-new Owner — this is the one creation path that doesn't go through
        // that screen. A cancel here is allowed to proceed anyway (there's no
        // fallback profile to bounce a first-run signup to); SelectMember's
        // VerifyOrEstablishAsync backstop will force setup the next time this
        // profile is switched into if it's still unprotected.
        if (await _authGate.PromptAndSetPinAsync(member)) member.DeviceAuthMode = DeviceAuthMode.Pin;

        await _repo.SaveAccountIndexAsync(new AccountIndex { Account = account, Members = { member }, Identities = { identity } });
        await _repo.SaveMemberDataAsync(accountId, memberId, new MemberData { Schedule = new Schedule { AccountId = accountId, MemberId = memberId } });

        try { await _identity.ClaimAsync(result.IdToken, accountId); }
        catch { /* the account still works locally; it just won't be found from another device until a retry succeeds */ }

        deviceIndex.AccountIds.Add(accountId);
        await _repo.SaveDeviceIndexAsync(deviceIndex);

        // Exactly one brand-new member, unambiguously the person who just signed
        // in — go straight to Home instead of making them tap their own new tile.
        var selected = await _session.SelectMemberAsync(accountId, memberId);
        if (selected is not null) await Shell.Current.GoToAsync("//home");
    }
}

public class MemberTileViewModel
{
    public Guid AccountId { get; }
    public Guid MemberId { get; }
    public string Name { get; }
    public string Initial { get; }
    public Color AvatarColor { get; }
    public bool UsesPhoto { get; }
    public ImageSource? AvatarPhoto { get; }
    public DeviceAuthMode DeviceAuthMode { get; }
    public IRelayCommand<MemberTileViewModel> SelectCommand { get; }

    public MemberTileViewModel(Guid accountId, Member member, IRelayCommand<MemberTileViewModel> selectCommand, IWorkoutRepository repo)
    {
        AccountId = accountId;
        MemberId = member.Id;
        Name = member.DisplayName;
        Initial = member.Initial;
        AvatarColor = Color.FromArgb(member.AvatarColor);
        DeviceAuthMode = member.DeviceAuthMode;
        SelectCommand = selectCommand;

        UsesPhoto = member.AvatarDisplay == AvatarDisplay.Photo && member.AvatarPhotoBlobFileName is not null;
        if (UsesPhoto)
        {
            var blobFileName = member.AvatarPhotoBlobFileName!;
            var memberId = member.Id;
            AvatarPhoto = ImageSource.FromStream(async _ =>
            {
                var bytes = await repo.GetProgressPhotoBlobAsync(accountId, memberId, blobFileName);
                return bytes is null ? null : new MemoryStream(bytes);
            });
        }
    }
}
