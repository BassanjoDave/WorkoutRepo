using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;
using Visibility = WorkoutTracker.Models.Visibility;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// Lets a member copy exercises (or HIIT sections) from one of their other
/// routines straight into the one they're currently building — a third way to
/// populate a ritual, alongside the manual Add form and Browse Exercises.
/// Reads/writes through IActiveRoutineBuilderContext so the copied items land
/// in the exact same live ExerciseRows/Sections collection the main builder
/// page is already showing (see StandardAddExercisePage/HiitAddSectionPage).
/// </summary>
public partial class AddFromRitualViewModel : ObservableObject
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;
    private readonly IActiveRoutineBuilderContext _context;

    private List<RoutineDefinition> _allRoutines = new();
    private List<Exercise> _allExercises = new();
    private Guid _activeMemberId;
    private bool _isHiit;
    private Guid? _excludeRoutineId;
    private RoutineDefinition? _pendingRoutine;

    [ObservableProperty] public partial bool ShowMine { get; set; } = true;
    [ObservableProperty] public partial bool ShowAccount { get; set; } = true;
    [ObservableProperty] public partial bool ShowManufacturer { get; set; } = true;
    [ObservableProperty] public partial ObservableCollection<RitualPickRowViewModel> Routines { get; set; } = new();
    [ObservableProperty] public partial bool HasSelectedRoutine { get; set; }
    [ObservableProperty] public partial string SelectedRoutineName { get; set; } = "";
    [ObservableProperty] public partial ObservableCollection<RitualItemCheckRowViewModel> Items { get; set; } = new();

    public AddFromRitualViewModel(IActiveSessionService session, IWorkoutRepository repo, IActiveRoutineBuilderContext context)
    {
        _session = session;
        _repo = repo;
        _context = context;
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
        _activeMemberId = member.Id;
        _isHiit = _context.ActiveHiit is not null;
        _excludeRoutineId = _context.ActiveStandard?.EditingRoutineId ?? _context.ActiveHiit?.EditingRoutineId;

        var shared = await _repo.GetSharedLibraryAsync(account.Id);
        var manufacturer = await _repo.GetManufacturerLibraryAsync();
        _allRoutines = shared.Routines.Concat(manufacturer.Routines).ToList();
        _allExercises = shared.Exercises.Concat(manufacturer.Exercises).ToList();

        Rebuild();
    }

    partial void OnShowMineChanged(bool value) => Rebuild();
    partial void OnShowAccountChanged(bool value) => Rebuild();
    partial void OnShowManufacturerChanged(bool value) => Rebuild();

    [RelayCommand] private void ToggleMine() => ShowMine = !ShowMine;
    [RelayCommand] private void ToggleAccount() => ShowAccount = !ShowAccount;
    [RelayCommand] private void ToggleManufacturer() => ShowManufacturer = !ShowManufacturer;

    private bool VisibilityOk(RoutineDefinition r)
    {
        if (r.Visibility == Visibility.Manufacturer) return ShowManufacturer;
        if (r.OwnerMemberId == _activeMemberId) return ShowMine;
        if (r.Visibility == Visibility.Account) return ShowAccount;
        return false;
    }

    private void Rebuild()
    {
        var filtered = _allRoutines
            .Where(r => r.Type == (_isHiit ? RoutineType.Hiit : RoutineType.Standard))
            .Where(r => r.Id != _excludeRoutineId)
            .Where(VisibilityOk)
            .OrderBy(r => r.Name)
            .ToList();

        var rows = new ObservableCollection<RitualPickRowViewModel>();
        foreach (var r in filtered)
        {
            var count = _isHiit ? r.Sections?.Count ?? 0 : r.Exercises.Count;
            var noun = _isHiit ? "sections" : "exercises";
            rows.Add(new RitualPickRowViewModel(r.Id, r.Name, $"{count} {noun}"));
        }
        Routines = rows;
    }

    public void SelectRoutine(Guid routineId)
    {
        var routine = _allRoutines.FirstOrDefault(r => r.Id == routineId);
        if (routine is null) return;
        _pendingRoutine = routine;
        SelectedRoutineName = routine.Name;
        HasSelectedRoutine = true;

        var items = new ObservableCollection<RitualItemCheckRowViewModel>();
        if (_isHiit)
        {
            foreach (var s in routine.Sections ?? new())
                items.Add(new RitualItemCheckRowViewModel(s.Title));
        }
        else
        {
            foreach (var target in routine.Exercises)
            {
                var name = _allExercises.FirstOrDefault(x => x.Id == target.ExerciseId)?.Name ?? "Exercise";
                items.Add(new RitualItemCheckRowViewModel(name));
            }
        }
        Items = items;
    }

    [RelayCommand]
    private void BackToRoutineList()
    {
        _pendingRoutine = null;
        HasSelectedRoutine = false;
        Items = new();
    }

    [RelayCommand]
    private async Task AddSelected()
    {
        if (_pendingRoutine is not null)
        {
            if (_isHiit)
            {
                var sections = _pendingRoutine.Sections ?? new();
                for (var i = 0; i < sections.Count && i < Items.Count; i++)
                {
                    if (Items[i].IsSelected) _context.ActiveHiit?.AddSectionFromTarget(sections[i]);
                }
            }
            else
            {
                var exercises = _pendingRoutine.Exercises;
                for (var i = 0; i < exercises.Count && i < Items.Count; i++)
                {
                    if (Items[i].IsSelected) _context.ActiveStandard?.AddExerciseFromTarget(exercises[i]);
                }
            }
        }
        await Shell.Current.GoToAsync("..");
    }
}

public class RitualPickRowViewModel
{
    public Guid Id { get; }
    public string Name { get; }
    public string Summary { get; }

    public RitualPickRowViewModel(Guid id, string name, string summary)
    {
        Id = id;
        Name = name;
        Summary = summary;
    }
}

public partial class RitualItemCheckRowViewModel : ObservableObject
{
    public string Name { get; }
    [ObservableProperty] public partial bool IsSelected { get; set; }

    public RitualItemCheckRowViewModel(string name)
    {
        Name = name;
    }
}
