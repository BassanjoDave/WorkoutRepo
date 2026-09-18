using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.ViewModels;

public partial class RemindersViewModel : ObservableObject
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;
    private readonly IWorkoutReminderService _reminders;

    private Guid _accountId;
    private Guid _memberId;
    private MemberData _memberData = new();
    private SharedLibrary _shared = new();
    private ManufacturerLibrary _manufacturer = new();
    private bool _suppressPermissionCheck;

    [ObservableProperty] public partial bool Enabled { get; set; }

    public bool IsSupported => _reminders.IsSupported;

    public RemindersViewModel(IActiveSessionService session, IWorkoutRepository repo, IWorkoutReminderService reminders)
    {
        _session = session;
        _repo = repo;
        _reminders = reminders;
    }

    partial void OnEnabledChanged(bool value)
    {
        if (_suppressPermissionCheck || !value) return;
        if (!IsSupported)
        {
            _suppressPermissionCheck = true;
            Enabled = false;
            _suppressPermissionCheck = false;
            var page = Shell.Current?.CurrentPage;
            if (page is not null)
            {
                _ = page.DisplayAlertAsync("Not available here",
                    "Workout reminders need Android or iOS — this device/platform doesn't support local notifications through this app yet.", "OK");
            }
            return;
        }
        _ = EnsurePermissionAsync();
    }

    private async Task EnsurePermissionAsync()
    {
        var granted = await _reminders.RequestPermissionAsync();
        if (granted) return;

        _suppressPermissionCheck = true;
        Enabled = false;
        _suppressPermissionCheck = false;

        var page = Shell.Current?.CurrentPage;
        if (page is not null)
        {
            await page.DisplayAlertAsync("Permission needed",
                "Notifications are turned off for this app. Enable them in your device's system settings to get workout reminders.", "OK");
        }
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
        _accountId = account.Id;
        _memberId = member.Id;

        _shared = await _repo.GetSharedLibraryAsync(account.Id);
        _manufacturer = await _repo.GetManufacturerLibraryAsync();
        _memberData = await _repo.GetMemberDataAsync(account.Id, member.Id);

        _suppressPermissionCheck = true;
        Enabled = _memberData.Reminders.Enabled;
        _suppressPermissionCheck = false;
    }

    private string? FindRoutineName(Guid routineId) =>
        _shared.Routines.FirstOrDefault(r => r.Id == routineId)?.Name
        ?? _manufacturer.Routines.FirstOrDefault(r => r.Id == routineId)?.Name;

    [RelayCommand]
    private async Task Save()
    {
        _memberData.Reminders.Enabled = Enabled;
        await _repo.SaveMemberDataAsync(_accountId, _memberId, _memberData);

        await _reminders.RescheduleAllAsync(_memberData, _accountId, _memberId, FindRoutineName);
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task Cancel() => await Shell.Current.GoToAsync("..");
}
