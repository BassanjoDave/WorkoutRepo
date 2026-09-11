using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// Assigns one routine per AM/PM slot per weekday. The underlying Schedule
/// model keeps a list per slot (RoutineDefinition already supports more than
/// one), but this pass's UI only ever writes zero or one — multi-routine
/// slots are deferred rather than half-built.
/// </summary>
public partial class ScheduleEditorViewModel : ObservableObject
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;

    private Guid _accountId;
    private Guid _memberId;
    private MemberData _memberData = new();

    [ObservableProperty] public partial ObservableCollection<ScheduleDayRowViewModel> Days { get; set; } = new();

    public ScheduleEditorViewModel(IActiveSessionService session, IWorkoutRepository repo)
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

        var shared = await _repo.GetSharedLibraryAsync(account.Id);
        var manufacturer = await _repo.GetManufacturerLibraryAsync();
        var options = new List<RoutineOption> { new(null, "Rest") };
        options.AddRange(shared.Routines.Concat(manufacturer.Routines)
            .Where(r => r.Type == RoutineType.Standard)
            .Select(r => new RoutineOption(r.Id, r.Name)));

        _memberData = await _repo.GetMemberDataAsync(account.Id, member.Id);

        // Replacing the whole collection (rather than Clear() + Add() in place) avoids
        // BindableLayout briefly seeing an empty source mid-rebuild — that transient
        // empty state crashes natively inside WinUI's own child-collection handling
        // (see the same fix in WorkoutsViewModel.Rebuild()).
        var days = new ObservableCollection<ScheduleDayRowViewModel>();
        foreach (Weekday day in Enum.GetValues<Weekday>())
        {
            _memberData.Schedule.Days.TryGetValue(day, out var slots);
            var amSelected = options.FirstOrDefault(o => o.Id == slots?.Am.FirstOrDefault()) ?? options[0];
            var pmSelected = options.FirstOrDefault(o => o.Id == slots?.Pm.FirstOrDefault()) ?? options[0];
            days.Add(new ScheduleDayRowViewModel(day, options, amSelected, pmSelected));
        }
        Days = days;
    }

    [RelayCommand]
    private async Task Save()
    {
        _memberData.Schedule.Days.Clear();
        _memberData.Schedule.UpdatedAt = DateTimeOffset.UtcNow;
        foreach (var day in Days)
        {
            _memberData.Schedule.Days[day.Weekday] = new DaySlots
            {
                Am = day.SelectedAm?.Id is Guid amId ? new() { amId } : new(),
                Pm = day.SelectedPm?.Id is Guid pmId ? new() { pmId } : new(),
            };
        }
        await _repo.SaveMemberDataAsync(_accountId, _memberId, _memberData);
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task Cancel() => await Shell.Current.GoToAsync("..");
}

public record RoutineOption(Guid? Id, string Name)
{
    public override string ToString() => Name;
}

public partial class ScheduleDayRowViewModel : ObservableObject
{
    public Weekday Weekday { get; }
    public string DayLabel { get; }
    public List<RoutineOption> Options { get; }

    [ObservableProperty] public partial RoutineOption? SelectedAm { get; set; }
    [ObservableProperty] public partial RoutineOption? SelectedPm { get; set; }

    public ScheduleDayRowViewModel(Weekday weekday, List<RoutineOption> options, RoutineOption selectedAm, RoutineOption selectedPm)
    {
        Weekday = weekday;
        DayLabel = weekday.ToString();
        Options = options;
        SelectedAm = selectedAm;
        SelectedPm = selectedPm;
    }
}
