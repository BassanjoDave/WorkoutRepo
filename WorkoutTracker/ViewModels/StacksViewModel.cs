using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// The Stacks tab: every supplement/medication stack the member has set up, with
/// a quick "mark today taken" toggle per stack — logged to MemberData.StackLog,
/// which HistoryViewModel's Stacks tab reads back for an adherence history.
/// </summary>
public partial class StacksViewModel : ObservableObject
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;
    private readonly IStackReminderService _reminders;
    private readonly IEntitlementService _entitlements;

    private Guid _accountId;
    private Guid _memberId;
    private MemberData _memberData = new();

    [ObservableProperty] public partial ObservableCollection<StackRowViewModel> Stacks { get; set; } = new();
    [ObservableProperty] public partial bool HasFullAccess { get; set; }

    public StacksViewModel(IActiveSessionService session, IWorkoutRepository repo, IStackReminderService reminders, IEntitlementService entitlements)
    {
        _session = session;
        _repo = repo;
        _reminders = reminders;
        _entitlements = entitlements;
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
        // Without Full Access, history still loads and displays normally below —
        // only adding a new stack is gated (see HasFullAccess and the upgrade
        // nudge in StacksPage.xaml).
        HasFullAccess = _entitlements.HasPageAccess(account, member.Id, PageEntitlements.Stacks);
        _accountId = account.Id;
        _memberId = member.Id;

        _memberData = await _repo.GetMemberDataAsync(account.Id, member.Id);
        Rebuild();
    }

    private void Rebuild()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        // Replacing the whole collection (rather than Clear() + Add() in place) avoids
        // BindableLayout briefly seeing an empty source mid-rebuild — that transient
        // empty state crashes natively inside WinUI's own child-collection handling
        // (see the same fix in WorkoutsViewModel.Rebuild()).
        var rows = new ObservableCollection<StackRowViewModel>();
        foreach (var stack in _memberData.Stacks.OrderBy(s => s.Time))
        {
            var takenToday = _memberData.StackLog.Any(l => l.StackId == stack.Id && l.Date == today && l.Taken);
            rows.Add(new StackRowViewModel(stack, takenToday, ToggleTakenCommand, EditCommand, DeleteCommand));
        }
        Stacks = rows;
    }

    [RelayCommand]
    private async Task AddStack() => await Shell.Current.GoToAsync("stackEditor");

    [RelayCommand]
    private async Task OpenUpgrade() => await Shell.Current.GoToAsync($"upgrade?page={PageEntitlements.Stacks}");

    [RelayCommand]
    private async Task Edit(Guid stackId) => await Shell.Current.GoToAsync($"stackEditor?stackId={stackId}");

    [RelayCommand]
    private async Task ToggleTaken(Guid stackId)
    {
        var stack = _memberData.Stacks.FirstOrDefault(s => s.Id == stackId);
        if (stack is null) return;

        var today = DateOnly.FromDateTime(DateTime.Today);
        var entry = _memberData.StackLog.FirstOrDefault(l => l.StackId == stackId && l.Date == today);
        var wasTaken = entry?.Taken ?? false;
        var nowTaken = !wasTaken;

        if (entry is null)
        {
            entry = new StackLogEntry { StackId = stackId, Date = today };
            _memberData.StackLog.Add(entry);
        }
        entry.Taken = nowTaken;
        entry.TakenAt = nowTaken ? DateTimeOffset.UtcNow : null;

        await _repo.SaveMemberDataAsync(_accountId, _memberId, _memberData);
        Rebuild();
    }

    [RelayCommand]
    private async Task Delete(Guid stackId)
    {
        var stack = _memberData.Stacks.FirstOrDefault(s => s.Id == stackId);
        if (stack is null) return;
        if (Shell.Current?.CurrentPage is Page page)
        {
            var confirmed = await page.DisplayAlertAsync("Delete stack", $"Delete \"{stack.Name}\"? This can't be undone.", "Delete", "Cancel");
            if (!confirmed) return;
        }

        // Must happen before removing the stack from the list — RescheduleAllAsync
        // only knows about stacks it's handed, so a deleted stack's own already-
        // scheduled reminder has to be cancelled here, by name, while it's still in hand.
        await _reminders.CancelForStackAsync(stack);

        _memberData.Stacks.Remove(stack);
        _memberData.StackLog.RemoveAll(l => l.StackId == stackId);
        await _repo.SaveMemberDataAsync(_accountId, _memberId, _memberData);
        Rebuild();
    }
}

public class StackRowViewModel
{
    public Guid Id { get; }
    public string Name { get; }
    public string TimeLabel { get; }
    public string DaysLabel { get; }
    public string ItemsSummary { get; }
    public bool TakenToday { get; }
    public string TakenButtonText => TakenToday ? "✓ Taken today" : "Mark as taken";
    public IAsyncRelayCommand<Guid> ToggleTakenCommand { get; }
    public IAsyncRelayCommand<Guid> EditCommand { get; }
    public IAsyncRelayCommand<Guid> DeleteCommand { get; }

    public StackRowViewModel(SupplementStack stack, bool takenToday,
        IAsyncRelayCommand<Guid> toggleTaken, IAsyncRelayCommand<Guid> edit, IAsyncRelayCommand<Guid> delete)
    {
        Id = stack.Id;
        Name = stack.Name;
        TimeLabel = stack.Time.ToString("h:mm tt");
        DaysLabel = DaysLabelFor(stack.Days);
        ItemsSummary = stack.Items.Count == 0 ? "No items yet" : string.Join(", ", stack.Items.Select(i => i.Name));
        TakenToday = takenToday;
        ToggleTakenCommand = toggleTaken;
        EditCommand = edit;
        DeleteCommand = delete;
    }

    private static readonly Weekday[] WeekOrder =
        { Weekday.Sunday, Weekday.Monday, Weekday.Tuesday, Weekday.Wednesday, Weekday.Thursday, Weekday.Friday, Weekday.Saturday };

    private static string DaysLabelFor(HashSet<Weekday> days)
    {
        if (days.Count == 0 || days.Count == 7) return "Every day";
        return string.Join(", ", WeekOrder.Where(days.Contains).Select(d => d.ToString()[..3]));
    }
}
