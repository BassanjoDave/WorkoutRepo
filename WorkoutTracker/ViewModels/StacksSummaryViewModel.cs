using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// Home dashboard card for the Stacks module — one bordered slot per stack
/// scheduled for today, up to two, with a "+N" tile for the rest (see
/// HomeViewModel/HomePage for the general module-card pattern this follows).
/// Respects the "hide medications" per-member display preference stored in
/// HomeModuleConfig.SummaryVariant for the "stacks" module (edited from
/// Customize Home) — filters medication-kind items out of what's shown here,
/// without touching the underlying data. HomePage.xaml.cs decides whether this
/// view or a locked-state placeholder gets shown at all — this ViewModel only
/// ever loads when access is confirmed.
/// </summary>
public partial class StacksSummaryViewModel : ObservableObject
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;

    [ObservableProperty] public partial ObservableCollection<StackSlotViewModel> VisibleSlots { get; set; } = new();
    [ObservableProperty] public partial int MoreCount { get; set; }
    [ObservableProperty] public partial bool HasAnyToday { get; set; }

    public bool HasMore => MoreCount > 0;
    partial void OnMoreCountChanged(int value) => OnPropertyChanged(nameof(HasMore));

    public StacksSummaryViewModel(IActiveSessionService session, IWorkoutRepository repo)
    {
        _session = session;
        _repo = repo;
    }

    public async Task LoadAsync()
    {
        var account = _session.ActiveAccount;
        var member = _session.ActiveMember;
        if (account is null || member is null) return;

        var memberData = await _repo.GetMemberDataAsync(account.Id, member.Id);
        var hideMedications = memberData.HomeModules.FirstOrDefault(m => m.ModuleId == "stacks")?.SummaryVariant == "hideMeds";

        var today = (Weekday)DateTime.Today.DayOfWeek;
        var todayDate = DateOnly.FromDateTime(DateTime.Today);
        var eligible = memberData.Stacks
            .Where(s => s.Days.Count == 0 || s.Days.Contains(today))
            .OrderBy(s => s.Time)
            .Select(s => (Stack: s, Items: hideMedications ? s.Items.Where(i => i.Kind != "Medication").ToList() : s.Items))
            // A stack that's entirely medications drops out of the card completely
            // once medications are hidden — nothing left to show for it today.
            .Where(t => t.Items.Count > 0)
            .ToList();

        var slots = new ObservableCollection<StackSlotViewModel>();
        foreach (var (stack, items) in eligible.Take(2))
        {
            var takenToday = memberData.StackLog.Any(l => l.StackId == stack.Id && l.Date == todayDate && l.Taken);
            slots.Add(new StackSlotViewModel(stack.Name, stack.Time.ToString("h:mm tt"), items.Count, takenToday));
        }
        VisibleSlots = slots;
        MoreCount = Math.Max(0, eligible.Count - slots.Count);
        HasAnyToday = eligible.Count > 0;
    }

    [RelayCommand]
    private async Task Open() => await Shell.Current.GoToAsync("//stacks");
}

public class StackSlotViewModel
{
    public string Name { get; }
    public string TimeLabel { get; }
    public string ItemsLabel { get; }
    public bool IsTaken { get; }

    public StackSlotViewModel(string name, string timeLabel, int itemCount, bool isTaken)
    {
        Name = name;
        TimeLabel = timeLabel;
        ItemsLabel = itemCount == 1 ? "1 item" : $"{itemCount} items";
        IsTaken = isTaken;
    }
}
