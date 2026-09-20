using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;
using Visibility = WorkoutTracker.Models.Visibility;

namespace WorkoutTracker.ViewModels;

public enum RoutineTypeFilter { All, Standard, Hiit }

public partial class WorkoutsViewModel : ObservableObject
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;
    private readonly IHomeBrowseContext _browseContext;

    /// <summary>Set for the duration of this one visit to the Rituals tab when it
    /// was reached via Home's "Browse Workouts" on a non-today day — see
    /// IHomeBrowseContext. Consumed (not re-read) in LoadAsync so it only ever
    /// applies to the visit it was set for, not some later unrelated one.</summary>
    public DateOnly? PendingBrowseDate { get; private set; }

    private List<RoutineDefinition> _allRoutines = new();
    private List<Exercise> _allExercises = new();
    private Dictionary<Guid, RoutineSchedule> _routineSchedules = new();
    private Guid _activeMemberId;
    private Guid _accountId;
    private string _memberName = "";
    private SharedLibrary _shared = new();

    [ObservableProperty] public partial RoutineTypeFilter TypeFilter { get; set; } = RoutineTypeFilter.All;
    [ObservableProperty] public partial bool IsAllActive { get; set; } = true;
    [ObservableProperty] public partial bool IsStandardActive { get; set; }
    [ObservableProperty] public partial bool IsHiitActive { get; set; }
    [ObservableProperty] public partial bool ShowMine { get; set; } = true;
    [ObservableProperty] public partial bool ShowAccount { get; set; } = true;
    [ObservableProperty] public partial bool ShowManufacturer { get; set; } = true;
    [ObservableProperty] public partial string SearchText { get; set; } = "";
    [ObservableProperty] public partial ObservableCollection<RoutineRowViewModel> Routines { get; set; } = new();

    public WorkoutsViewModel(IActiveSessionService session, IWorkoutRepository repo, IHomeBrowseContext browseContext)
    {
        _session = session;
        _repo = repo;
        _browseContext = browseContext;
    }

    public async Task LoadAsync()
    {
        PendingBrowseDate = _browseContext.ConsumePendingDate();

        var account = _session.ActiveAccount;
        var member = _session.ActiveMember;
        if (account is null || member is null)
        {
            await Shell.Current.GoToAsync("//gate");
            return;
        }
        _activeMemberId = member.Id;
        _accountId = account.Id;
        _memberName = member.DisplayName;

        _shared = await _repo.GetSharedLibraryAsync(account.Id);
        var manufacturer = await _repo.GetManufacturerLibraryAsync();
        var memberData = await _repo.GetMemberDataAsync(account.Id, member.Id);

        _allRoutines = _shared.Routines.Concat(manufacturer.Routines).ToList();
        _allExercises = _shared.Exercises.Concat(manufacturer.Exercises).ToList();
        _routineSchedules = memberData.RoutineSchedules;

        Rebuild();
    }

    partial void OnTypeFilterChanged(RoutineTypeFilter value)
    {
        IsAllActive = value == RoutineTypeFilter.All;
        IsStandardActive = value == RoutineTypeFilter.Standard;
        IsHiitActive = value == RoutineTypeFilter.Hiit;
        Rebuild();
    }
    partial void OnShowMineChanged(bool value) => Rebuild();
    partial void OnShowAccountChanged(bool value) => Rebuild();
    partial void OnShowManufacturerChanged(bool value) => Rebuild();
    partial void OnSearchTextChanged(string value) => Rebuild();

    [RelayCommand]
    private void SetTypeFilter(string key) =>
        TypeFilter = key switch { "standard" => RoutineTypeFilter.Standard, "hiit" => RoutineTypeFilter.Hiit, _ => RoutineTypeFilter.All };

    [RelayCommand] private void ToggleMine() => ShowMine = !ShowMine;
    [RelayCommand] private void ToggleAccount() => ShowAccount = !ShowAccount;
    [RelayCommand] private void ToggleManufacturer() => ShowManufacturer = !ShowManufacturer;

    /// <summary>
    /// Forks a routine you don't own into a new Private one under your name,
    /// then opens it in the right builder so you can customize it immediately.
    /// The original — whether a manufacturer routine or another member's
    /// Account-shared one — is never touched.
    /// </summary>
    [RelayCommand]
    private async Task CopyToMine(Guid routineId)
    {
        var source = _allRoutines.FirstOrDefault(r => r.Id == routineId);
        if (source is null) return;

        var now = DateTimeOffset.UtcNow;
        var copy = new RoutineDefinition
        {
            Id = Guid.NewGuid(),
            AccountId = _accountId,
            OwnerMemberId = _activeMemberId,
            OwnerNameSnapshot = _memberName,
            SourceRoutineId = source.Id,
            SourceNameSnapshot = source.OwnerNameSnapshot is null ? source.Name : source.OwnerDisplayName,
            SourceExerciseKeysSnapshot = RoutineNamingHelper.ExerciseKeysFor(source.Type, source.Exercises.Select(e => e.ExerciseId), source.Sections),
            Name = source.Name,
            Visibility = Visibility.Private,
            Type = source.Type,
            Exercises = source.Exercises.Select(e => new RoutineExerciseTarget
            {
                ExerciseId = e.ExerciseId,
                Groups = e.Groups.Select(g => new SetGroupTarget { Sets = g.Sets, Reps = g.Reps, Weight = g.Weight }).ToList(),
                DurationSeconds = e.DurationSeconds, FrequencyHz = e.FrequencyHz, Level = e.Level,
                Stance = e.Stance, Mode = e.Mode, TimeMinutes = e.TimeMinutes, Resistance = e.Resistance,
            }).ToList(),
            Sections = source.Sections?.Select(s => new HiitSection
            {
                Id = Guid.NewGuid(), Type = s.Type, Title = s.Title, Description = s.Description,
                Seconds = s.Seconds, IsCycleRest = s.IsCycleRest,
            }).ToList(),
            CycleRepeats = source.CycleRepeats,
            RestBetweenCyclesSeconds = source.RestBetweenCyclesSeconds,
            UpdatedAt = now,
        };

        _shared.Routines.Add(copy);
        await _repo.SaveSharedLibraryAsync(_accountId, _shared);

        var route = copy.Type == RoutineType.Hiit ? "hiitBuilder" : "standardBuilder";
        await Shell.Current.GoToAsync($"{route}?routineId={copy.Id}");
    }

    private bool VisibilityOk(RoutineDefinition r)
    {
        if (r.Visibility == Visibility.Manufacturer) return ShowManufacturer;
        if (r.OwnerMemberId == _activeMemberId) return ShowMine;
        if (r.Visibility == Visibility.Account) return ShowAccount;
        return false; // Private and not owned by the viewer, or Community (not shipped yet)
    }

    private void Rebuild()
    {
        // Replacing the whole collection (rather than Clear() + Add() in place) avoids
        // BindableLayout briefly seeing an empty source mid-rebuild — that transient
        // empty state crashed natively inside WinUI's own child-collection handling
        // on this app's FlexLayout-hosted routine list.
        var needle = SearchText.Trim();
        var filtered = _allRoutines
            .Where(VisibilityOk)
            .Where(r => TypeFilter == RoutineTypeFilter.All
                || (TypeFilter == RoutineTypeFilter.Hiit ? r.Type == RoutineType.Hiit : r.Type != RoutineType.Hiit))
            .Where(r => string.IsNullOrEmpty(needle) || r.Name.Contains(needle, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => Score(r))
            .ToList();

        var rows = new ObservableCollection<RoutineRowViewModel>();
        foreach (var r in filtered)
        {
            var exNames = r.Exercises.Select(e => _allExercises.FirstOrDefault(x => x.Id == e.ExerciseId)?.Name ?? "Exercise").ToList();
            rows.Add(new RoutineRowViewModel
            {
                Id = r.Id,
                Name = r.Name,
                IsHiit = r.Type == RoutineType.Hiit,
                IsCustom = r.Visibility != Visibility.Manufacturer,
                // Editing someone else's shared routine forks a copy (the visibility
                // review's copy-on-modify rule) rather than editing their original in place.
                CanEdit = r.OwnerMemberId == _activeMemberId,
                CanCopy = r.OwnerMemberId != _activeMemberId,
                OwnerBadge = OwnerBadge(r),
                SourceLabel = r.SourceNameSnapshot is string src ? $"Adapted from {src}" : null,
                ExerciseCountLabel = $"{exNames.Count} exercises",
                ScheduleTags = ScheduleTagsFor(r.Id),
            });
        }
        Routines = rows;
    }

    private int Score(RoutineDefinition r)
    {
        var score = 0;
        if (r.Visibility != Visibility.Manufacturer) score += 2;
        if (_routineSchedules.ContainsKey(r.Id)) score += 1;
        return score;
    }

    private string? OwnerBadge(RoutineDefinition r)
    {
        if (r.Visibility == Visibility.Manufacturer) return r.OwnerNameSnapshot is null ? "Manufacturer" : r.OwnerDisplayName;
        if (r.OwnerMemberId == _activeMemberId) return null;
        if (r.Visibility == Visibility.Account) return $"Shared · by {r.OwnerNameSnapshot ?? "Unknown"}";
        return null;
    }

    private List<string> ScheduleTagsFor(Guid routineId) =>
        _routineSchedules.TryGetValue(routineId, out var schedule)
            ? new List<string> { ScheduleResolver.RecurrenceSummary(schedule) }
            : new List<string>();
}

public class RoutineRowViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsHiit { get; set; }
    public bool IsCustom { get; set; }
    public bool CanEdit { get; set; }
    public bool CanCopy { get; set; }
    public string? OwnerBadge { get; set; }
    public string? SourceLabel { get; set; }
    public string ExerciseCountLabel { get; set; } = "";
    public List<string> ScheduleTags { get; set; } = new();
}
