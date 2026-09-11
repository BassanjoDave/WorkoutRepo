using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// The primary holder's management screen: roster, capability presets, invite
/// (stubbed — real redemption needs the Identity/auth backend from Part B),
/// and the junior split-off migration, wired to the actual
/// IMemberSplitOffService rather than just described.
/// </summary>
public partial class ManageMembersViewModel : ObservableObject
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;
    private readonly ISeatAvailabilityService _seats;
    private readonly IMemberSplitOffService _splitOff;
    private readonly IMemberAuthGateService _authGate;

    private AccountIndex? _accountIndex;

    /// <summary>Set once the "settings lockdown" PIN/biometric check (below) has
    /// passed for this page instance — Shell reuses the same ViewModel instance
    /// across a back-navigation onto an already-open page (e.g. Edit → Back), and
    /// LoadAsync re-runs every time OnAppearing fires, so without this a holder
    /// editing one member would be re-prompted on every trip back to this list.
    /// A genuinely fresh navigation to this route creates a new ViewModel instance
    /// (and thus a fresh false), so it re-prompts correctly.</summary>
    private bool _verifiedThisOpen;

    [ObservableProperty] public partial string SeatUsageLabel { get; set; } = "";
    [ObservableProperty] public partial string PremiumSeatUsageLabel { get; set; } = "";
    [ObservableProperty] public partial ObservableCollection<MemberRowViewModel> Members { get; set; } = new();

    public ManageMembersViewModel(IActiveSessionService session, IWorkoutRepository repo,
        ISeatAvailabilityService seats, IMemberSplitOffService splitOff, IMemberAuthGateService authGate)
    {
        _session = session;
        _repo = repo;
        _seats = seats;
        _splitOff = splitOff;
        _authGate = authGate;
    }

    public async Task LoadAsync()
    {
        var account = _session.ActiveAccount;
        var member = _session.ActiveMember;
        if (account is null || member is null || !member.EffectiveCapabilities().ManageMembers)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        _accountIndex = await _repo.GetAccountIndexAsync(account.Id);
        if (_accountIndex is null) return;

        // Settings lockdown: this is a holder-administrative screen, so it's gated by
        // the PRIMARY HOLDER's own protection setting regardless of who's currently
        // the active profile — closes the gap where anyone who can pick up an
        // already-unlocked device could otherwise reach it.
        if (!_verifiedThisOpen)
        {
            var holder = _accountIndex.Members.FirstOrDefault(m => m.Id == account.PrimaryHolderMemberId);
            if (holder is not null && !await _authGate.VerifyAsync(holder, "Open Manage Members"))
            {
                await Shell.Current.GoToAsync("..");
                return;
            }
            _verifiedThisOpen = true;
        }

        SeatUsageLabel = $"{_accountIndex.Members.Count(m => m.Status == MemberStatus.Active)} of {account.SeatLimit} seats used";
        PremiumSeatUsageLabel = $"{account.PremiumMemberIds.Count} of {account.PremiumSeatCount} premium seats assigned";
        var canManagePremiumSeats = member.Id == account.PrimaryHolderMemberId;

        // Replacing the whole collection (rather than Clear() + Add() in place) avoids
        // BindableLayout briefly seeing an empty source mid-rebuild — that transient
        // empty state crashes natively inside WinUI's own child-collection handling
        // (see the same fix in WorkoutsViewModel.Rebuild()).
        var members = new ObservableCollection<MemberRowViewModel>();
        foreach (var m in _accountIndex.Members.Where(m => m.Status == MemberStatus.Active))
        {
            members.Add(new MemberRowViewModel(
                m.Id,
                m.DisplayName,
                m.Initial,
                Color.FromArgb(m.AvatarColor),
                m.RolePreset.ToString(),
                isPrimaryHolder: m.Id == account.PrimaryHolderMemberId,
                canManagePremiumSeats: canManagePremiumSeats,
                isPremium: account.PremiumMemberIds.Contains(m.Id),
                // The holder never needs a "view/toggle my own photos" control
                // on their own row — that's just the Measurements page.
                showProgressPhotoControls: canManagePremiumSeats && m.Id != account.PrimaryHolderMemberId,
                progressPhotosVisibleToHolder: m.EffectiveProgressPhotosVisibleToHolder,
                editCommand: new AsyncRelayCommand(() => OpenEditAsync(m.Id)),
                removeCommand: new AsyncRelayCommand(() => RemoveAsync(m.Id)),
                splitOffCommand: new AsyncRelayCommand(() => SplitOffAsync(m.Id)),
                togglePremiumCommand: new AsyncRelayCommand(() => TogglePremiumAsync(m.Id)),
                toggleProgressPhotoVisibilityCommand: new AsyncRelayCommand(() => ToggleProgressPhotoVisibilityAsync(m.Id)),
                viewPhotosCommand: new AsyncRelayCommand(() => ViewPhotosAsync(m.Id))));
        }
        Members = members;
    }

    [RelayCommand]
    private async Task Back() => await Shell.Current.GoToAsync("..");

    [RelayCommand]
    private async Task AddDependent() => await Shell.Current.GoToAsync("memberEdit?mode=new");

    [RelayCommand]
    private async Task InviteMember()
    {
        if (_accountIndex is null) return;
        if (!_seats.HasCapacity(_accountIndex))
        {
            await Shell.Current.CurrentPage.DisplayAlertAsync("Seat limit reached",
                "This account is at its seat limit. Remove a member or upgrade the plan before inviting another.", "OK");
            return;
        }

        // Real invite redemption needs the Identity/auth backend from Part B —
        // this demonstrates the flow's shape without a server to redeem against.
        var code = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        await Shell.Current.CurrentPage.DisplayAlertAsync("Invite generated",
            $"Code {code} would be sent to the invitee. Redemption isn't wired up until the account backend exists.", "OK");
    }

    /// <summary>Assigns/unassigns one of the account's paid premium seats (ads removed,
    /// gated pages unlocked) to a member — restricted to the primary holder in the UI
    /// (see canManagePremiumSeats above). PremiumSeatCount itself is set server-side
    /// once real billing exists; until then it's set by hand in Developer Settings.</summary>
    private async Task TogglePremiumAsync(Guid memberId)
    {
        if (_accountIndex is null) return;
        var account = _accountIndex.Account;
        if (account.PremiumMemberIds.Contains(memberId))
        {
            account.PremiumMemberIds.Remove(memberId);
        }
        else
        {
            if (account.PremiumMemberIds.Count >= account.PremiumSeatCount)
            {
                await Shell.Current.CurrentPage.DisplayAlertAsync("No premium seats left",
                    $"This account has {account.PremiumSeatCount} premium seat(s) and all are assigned. " +
                    "Unassign one or upgrade for more seats before adding another.", "OK");
                return;
            }
            account.PremiumMemberIds.Add(memberId);
        }
        await _repo.SaveAccountIndexAsync(_accountIndex);
        await LoadAsync();
    }

    /// <summary>Flips whether the primary holder may view this member's progress
    /// photos (Measurements page) — see Member.EffectiveProgressPhotosVisibleToHolder.
    /// A content-privacy preference, not a billing seat, so no capacity check like
    /// TogglePremiumAsync's.</summary>
    private async Task ToggleProgressPhotoVisibilityAsync(Guid memberId)
    {
        if (_accountIndex is null) return;
        var target = _accountIndex.Members.FirstOrDefault(m => m.Id == memberId);
        if (target is null) return;
        target.ProgressPhotosVisibleToHolder = !target.EffectiveProgressPhotosVisibleToHolder;
        await _repo.SaveAccountIndexAsync(_accountIndex);
        await LoadAsync();
    }

    private async Task ViewPhotosAsync(Guid memberId) =>
        await Shell.Current.GoToAsync($"progressPhotoGallery?ownerMemberId={memberId}");

    private async Task OpenEditAsync(Guid memberId) =>
        await Shell.Current.GoToAsync($"memberEdit?mode=edit&memberId={memberId}");

    private async Task RemoveAsync(Guid memberId)
    {
        if (_accountIndex is null) return;
        var member = _accountIndex.Members.FirstOrDefault(m => m.Id == memberId);
        if (member is null) return;
        if (member.Id == _accountIndex.Account.PrimaryHolderMemberId)
        {
            await Shell.Current.CurrentPage.DisplayAlertAsync("Can't remove the holder",
                "The primary account holder can't be removed. Split them off or transfer ownership first.", "OK");
            return;
        }

        var confirmed = await Shell.Current.CurrentPage.DisplayAlertAsync("Remove member",
            $"Remove {member.DisplayName} from this account? Their history stays on record but they lose access.", "Remove", "Cancel");
        if (!confirmed) return;

        member.Status = MemberStatus.Removed;
        await _repo.SaveAccountIndexAsync(_accountIndex);
        await LoadAsync();
    }

    private async Task SplitOffAsync(Guid memberId)
    {
        if (_accountIndex is null) return;
        var member = _accountIndex.Members.FirstOrDefault(m => m.Id == memberId);
        if (member is null) return;
        if (member.Id == _accountIndex.Account.PrimaryHolderMemberId)
        {
            await Shell.Current.CurrentPage.DisplayAlertAsync("Can't split off the holder",
                "The primary account holder already owns this account.", "OK");
            return;
        }

        // A member with no linked sign-in of their own can't be split off into a
        // standalone account — nobody could ever authenticate as them to reach it
        // again afterward (see MemberSplitOffService's identity-carry-over comment).
        if (member.IdentityId is null)
        {
            await Shell.Current.CurrentPage.DisplayAlertAsync("Can't split off yet",
                $"{member.DisplayName} doesn't have a Google or email sign-in linked yet. Link one from their profile first, " +
                "so they'll be able to sign back into their own account afterward.", "OK");
            return;
        }

        var confirmed = await Shell.Current.CurrentPage.DisplayAlertAsync("Split off into a new account",
            $"{member.DisplayName} will get their own standalone account, carrying their full history and workouts with them. " +
            "This account loses them as a member. This can't be undone from here.", "Split off", "Cancel");
        if (!confirmed) return;

        var newAccountId = await _splitOff.SplitOffAsync(_accountIndex.Account.Id, memberId, $"{member.DisplayName}'s Account");
        await Shell.Current.CurrentPage.DisplayAlertAsync("Done",
            $"{member.DisplayName} now has their own account ({newAccountId.ToString()[..8]}…) with their full history intact.", "OK");
        await LoadAsync();
    }
}

public partial class MemberRowViewModel : ObservableObject
{
    public Guid Id { get; }
    public string Name { get; }
    public string Initial { get; }
    public Color AvatarColor { get; }
    public string RoleLabel { get; }
    public bool IsPrimaryHolder { get; }
    public bool CanManagePremiumSeats { get; }
    [ObservableProperty] public partial bool IsPremium { get; set; }
    public string PremiumLabel => IsPremium ? "Premium: On" : "Premium: Off";
    partial void OnIsPremiumChanged(bool value) => OnPropertyChanged(nameof(PremiumLabel));

    /// <summary>Only true on a non-holder row, for whoever is viewing as the primary holder — never shown on the holder's own row.</summary>
    public bool ShowProgressPhotoControls { get; }
    [ObservableProperty] public partial bool ProgressPhotosVisibleToHolder { get; set; }
    public string ProgressPhotoVisibilityLabel => ProgressPhotosVisibleToHolder ? "Progress Photos: Visible to Me" : "Progress Photos: Private";
    partial void OnProgressPhotosVisibleToHolderChanged(bool value)
    {
        OnPropertyChanged(nameof(ProgressPhotoVisibilityLabel));
        OnPropertyChanged(nameof(ShowViewPhotosButton));
    }
    public bool ShowViewPhotosButton => ShowProgressPhotoControls && ProgressPhotosVisibleToHolder;

    public IAsyncRelayCommand EditCommand { get; }
    public IAsyncRelayCommand RemoveCommand { get; }
    public IAsyncRelayCommand SplitOffCommand { get; }
    public IAsyncRelayCommand TogglePremiumCommand { get; }
    public IAsyncRelayCommand ToggleProgressPhotoVisibilityCommand { get; }
    public IAsyncRelayCommand ViewPhotosCommand { get; }

    public MemberRowViewModel(Guid id, string name, string initial, Color avatarColor, string roleLabel,
        bool isPrimaryHolder, bool canManagePremiumSeats, bool isPremium,
        bool showProgressPhotoControls, bool progressPhotosVisibleToHolder,
        IAsyncRelayCommand editCommand, IAsyncRelayCommand removeCommand, IAsyncRelayCommand splitOffCommand,
        IAsyncRelayCommand togglePremiumCommand, IAsyncRelayCommand toggleProgressPhotoVisibilityCommand,
        IAsyncRelayCommand viewPhotosCommand)
    {
        Id = id;
        Name = name;
        Initial = initial;
        AvatarColor = avatarColor;
        RoleLabel = roleLabel;
        IsPrimaryHolder = isPrimaryHolder;
        CanManagePremiumSeats = canManagePremiumSeats;
        IsPremium = isPremium;
        ShowProgressPhotoControls = showProgressPhotoControls;
        ProgressPhotosVisibleToHolder = progressPhotosVisibleToHolder;
        EditCommand = editCommand;
        RemoveCommand = removeCommand;
        SplitOffCommand = splitOffCommand;
        TogglePremiumCommand = togglePremiumCommand;
        ToggleProgressPhotoVisibilityCommand = toggleProgressPhotoVisibilityCommand;
        ViewPhotosCommand = viewPhotosCommand;
    }
}
