using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.ViewModels;

/// <summary>A flat, newest-first list of the active member's own notifications
/// (MemberData.Notifications) — no delete/mark-all/pagination in this first version.
/// See NotificationWriter for how entries get added.</summary>
public partial class NotificationsViewModel : ObservableObject
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;

    private Guid _accountId;
    private Guid _memberId;
    private MemberData _memberData = new();

    [ObservableProperty] public partial ObservableCollection<NotificationRowViewModel> Notifications { get; set; } = new();
    [ObservableProperty] public partial bool IsEmpty { get; set; }

    public NotificationsViewModel(IActiveSessionService session, IWorkoutRepository repo)
    {
        _session = session;
        _repo = repo;
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

        _memberData = await _repo.GetMemberDataAsync(account.Id, member.Id);
        Rebuild();
    }

    private void Rebuild()
    {
        // Replacing the whole collection (rather than Clear() + Add() in place) avoids
        // BindableLayout briefly seeing an empty source mid-rebuild — that transient
        // empty state crashes natively inside WinUI's own child-collection handling
        // (see the same fix in WorkoutsViewModel.Rebuild()).
        var rows = new ObservableCollection<NotificationRowViewModel>();
        foreach (var entry in _memberData.Notifications.OrderByDescending(n => n.CreatedAt))
        {
            rows.Add(new NotificationRowViewModel(entry, SelectCommand));
        }
        Notifications = rows;
        IsEmpty = Notifications.Count == 0;
    }

    [RelayCommand]
    private async Task Back() => await Shell.Current.GoToAsync("..");

    [RelayCommand]
    private async Task Select(NotificationRowViewModel row)
    {
        if (!row.Entry.IsRead)
        {
            row.Entry.IsRead = true;
            await _repo.SaveMemberDataAsync(_accountId, _memberId, _memberData);
            Rebuild();
        }
        if (row.Entry.DeepLinkRoute is string route) await Shell.Current.GoToAsync(route);
    }
}

public class NotificationRowViewModel
{
    public NotificationEntry Entry { get; }
    public string Title => Entry.Title;
    public string Body => Entry.Body;
    public bool IsRead => Entry.IsRead;
    public string TimeLabel => Entry.CreatedAt.LocalDateTime.ToString("MMM d, h:mm tt");
    public IAsyncRelayCommand<NotificationRowViewModel> SelectCommand { get; }

    public NotificationRowViewModel(NotificationEntry entry, IAsyncRelayCommand<NotificationRowViewModel> selectCommand)
    {
        Entry = entry;
        SelectCommand = selectCommand;
    }
}
