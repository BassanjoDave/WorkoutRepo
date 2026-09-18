using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// Read-mostly overview of every routine with a recurring schedule — one row
/// per routine showing its time + recurrence in plain English (see
/// ScheduleResolver.RecurrenceSummary). Tapping a row opens that routine's
/// own builder page, where the actual time+recurrence+snooze editor lives
/// (RoutineReminderEditorViewModel) — this page exists only to see everything
/// at a glance without opening each routine individually, not to duplicate
/// that editor.
/// </summary>
public partial class ScheduleEditorViewModel : ObservableObject
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;

    [ObservableProperty] public partial ObservableCollection<ScheduleOverviewRowViewModel> Rows { get; set; } = new();
    [ObservableProperty] public partial bool IsEmpty { get; set; }

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

        var shared = await _repo.GetSharedLibraryAsync(account.Id);
        var manufacturer = await _repo.GetManufacturerLibraryAsync();
        RoutineDefinition? FindRoutine(Guid id) =>
            shared.Routines.FirstOrDefault(r => r.Id == id) ?? manufacturer.Routines.FirstOrDefault(r => r.Id == id);

        var memberData = await _repo.GetMemberDataAsync(account.Id, member.Id);

        // Replacing the whole collection (rather than Clear() + Add() in place) avoids
        // BindableLayout briefly seeing an empty source mid-rebuild — that transient
        // empty state crashes natively inside WinUI's own child-collection handling
        // (see the same fix in WorkoutsViewModel.Rebuild()).
        var rows = new ObservableCollection<ScheduleOverviewRowViewModel>();
        foreach (var (routineId, schedule) in memberData.RoutineSchedules)
        {
            var routine = FindRoutine(routineId);
            if (routine is null) continue;
            rows.Add(new ScheduleOverviewRowViewModel(routineId, routine.Name, routine.Type == RoutineType.Hiit,
                ScheduleResolver.RecurrenceSummary(schedule), OpenRoutineCommand));
        }
        Rows = rows;
        IsEmpty = Rows.Count == 0;
    }

    [RelayCommand]
    private async Task OpenRoutine(ScheduleOverviewRowViewModel row)
    {
        var route = row.IsHiit ? "hiitBuilder" : "standardBuilder";
        await Shell.Current.GoToAsync($"{route}?routineId={row.RoutineId}");
    }

    [RelayCommand]
    private async Task Back() => await Shell.Current.GoToAsync("..");
}

public class ScheduleOverviewRowViewModel
{
    public Guid RoutineId { get; }
    public string RoutineName { get; }
    public bool IsHiit { get; }
    public string Summary { get; }
    public IAsyncRelayCommand<ScheduleOverviewRowViewModel> OpenCommand { get; }

    public ScheduleOverviewRowViewModel(Guid routineId, string routineName, bool isHiit, string summary, IAsyncRelayCommand<ScheduleOverviewRowViewModel> openCommand)
    {
        RoutineId = routineId;
        RoutineName = routineName;
        IsHiit = isHiit;
        Summary = summary;
        OpenCommand = openCommand;
    }
}
