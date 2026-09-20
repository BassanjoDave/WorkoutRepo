using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;
using WorkoutTracker.Views;
using Visibility = WorkoutTracker.Models.Visibility;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// Standard (sets/reps/weight) workout builder. Any exercise — including
/// Vibration Plate and Cardio — can be added to a routine here; those rows
/// let the author set default stance/duration/frequency/level (or
/// time/resistance for Cardio) so a fresh session has something to pre-fill
/// from, the same way standard exercises pre-fill sets/reps from authored groups.
/// </summary>
public partial class StandardBuilderViewModel : ObservableObject, IQueryAttributable
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;
    private readonly IWorkoutReminderService _reminders;
    private readonly IHomeWorkoutBridge _homeWorkoutBridge;
    private readonly IPendingExerciseBridge _pendingExerciseBridge;
    private readonly IActiveRoutineBuilderContext _builderContext;

    /// <summary>Sentinel row appended to AvailableExercises so the picker can offer "+ Add custom exercise…" — see OnSelectedExerciseToAddChanged.</summary>
    private static readonly Guid AddCustomExerciseSentinelId = Guid.Empty;

    private Guid? _editingRoutineId;
    private bool _fromHome;
    private bool _isManufacturerFork;
    private Guid _routineId;
    private Guid _accountId;
    private Guid _memberId;
    private string _memberName = "";
    private SharedLibrary _shared = new();
    private ManufacturerLibrary _manufacturer = new();
    private MemberData _memberData = new();
    private List<Exercise> _allExercises = new();
    private List<Rig> _rigs = new();

    /// <summary>The routine id being edited, if any — used by AddFromRitualPage to
    /// exclude this routine from its own "copy exercises from" list.</summary>
    public Guid? EditingRoutineId => _editingRoutineId;

    public RoutineReminderEditorViewModel Reminder { get; }

    /// <summary>
    /// Set only when opened from a running session's Edit button — see
    /// SessionViewModel.EditWorkout. When present, Save asks whether the
    /// change applies to just this date/time (forks a private copy and
    /// one-time-assigns it) or to every future occurrence (edits the shared
    /// routine as normal).
    /// </summary>
    private DateOnly? _occDate;
    private TimeOnly? _occTime;

    // Field, not property, would silently break XAML binding — see HiitBuilderViewModel.SectionTypes.
    private static readonly string[] Categories =
        { "All", "Chest", "Shoulders", "Back", "Arms", "Abs", "Legs", "Full Body", "Cardio", "Vibration Plate" };
    public string[] CategoryOptions => Categories;

    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial string RoutineName { get; set; } = "New Workout";
    [ObservableProperty] public partial bool IsAccountShared { get; set; }
    [ObservableProperty] public partial ObservableCollection<StandardExerciseRowViewModel> ExerciseRows { get; set; } = new();
    [ObservableProperty] public partial string SelectedCategory { get; set; } = "All";
    [ObservableProperty] public partial ObservableCollection<ExercisePickerOption> AvailableExercises { get; set; } = new();
    [ObservableProperty] public partial ExercisePickerOption? SelectedExerciseToAdd { get; set; }

    // Staged detail fields for the exercise about to be added — filled in here, in the
    // Add Exercise card itself, rather than only after it lands in the list below.
    public bool AddingStandard => SelectedExerciseToAdd?.Kind == SessionRowKind.Standard;
    public bool AddingVibration => SelectedExerciseToAdd?.Kind == SessionRowKind.Vibration;
    public bool AddingCardio => SelectedExerciseToAdd?.Kind == SessionRowKind.Cardio;
    public bool AddingHasWeight => SelectedExerciseToAdd?.HasWeight ?? false;
    public string[] PendingStanceOptions => VibrationOptions.Stances;
    public string[] PendingModeOptions => VibrationOptions.Modes;

    [ObservableProperty] public partial string PendingSets { get; set; } = "";
    [ObservableProperty] public partial string PendingReps { get; set; } = "";
    [ObservableProperty] public partial string PendingWeight { get; set; } = "";
    [ObservableProperty] public partial string PendingStance { get; set; } = "";
    [ObservableProperty] public partial string PendingMode { get; set; } = "";
    [ObservableProperty] public partial string PendingDuration { get; set; } = "";
    [ObservableProperty] public partial string PendingFrequency { get; set; } = "";
    [ObservableProperty] public partial string PendingLevel { get; set; } = "";
    [ObservableProperty] public partial string PendingTime { get; set; } = "";
    [ObservableProperty] public partial string PendingResistance { get; set; } = "";

    public StandardBuilderViewModel(IActiveSessionService session, IWorkoutRepository repo, IWorkoutReminderService reminders,
        IHomeWorkoutBridge homeWorkoutBridge, IPendingExerciseBridge pendingExerciseBridge, IActiveRoutineBuilderContext builderContext)
    {
        _session = session;
        _repo = repo;
        _reminders = reminders;
        _homeWorkoutBridge = homeWorkoutBridge;
        _pendingExerciseBridge = pendingExerciseBridge;
        _builderContext = builderContext;
        Reminder = new RoutineReminderEditorViewModel(reminders);
    }

    partial void OnSelectedCategoryChanged(string value) => RebuildAvailableExercises();

    /// <summary>Picking the "+ Add custom exercise…" row pushes the exercise editor instead of adding anything — resets the picker so that row doesn't linger as "selected" while the member is away.
    /// Also clears any staged detail fields from the previous selection, so they don't leak into the next exercise's Add card.</summary>
    partial void OnSelectedExerciseToAddChanged(ExercisePickerOption? value)
    {
        OnPropertyChanged(nameof(AddingStandard));
        OnPropertyChanged(nameof(AddingVibration));
        OnPropertyChanged(nameof(AddingCardio));
        OnPropertyChanged(nameof(AddingHasWeight));
        PendingSets = ""; PendingReps = ""; PendingWeight = "";
        PendingStance = ""; PendingMode = ""; PendingDuration = ""; PendingFrequency = ""; PendingLevel = "";
        PendingTime = ""; PendingResistance = "";

        if (value is null || value.Id != AddCustomExerciseSentinelId) return;
        SelectedExerciseToAdd = null;
        _ = Shell.Current.GoToAsync("exerciseEditor");
    }

    private static string CategoryLabel(ExerciseCategory c) => c switch
    {
        ExerciseCategory.FullBody => "Full Body",
        ExerciseCategory.VibrationPlate => "Vibration Plate",
        _ => c.ToString(),
    };

    private void RebuildAvailableExercises()
    {
        var previousSelection = SelectedExerciseToAdd?.Id;
        AvailableExercises.Clear();
        var filtered = _allExercises
            .Where(e => SelectedCategory == "All" || CategoryLabel(e.Category) == SelectedCategory)
            .OrderBy(e => e.Name);
        foreach (var e in filtered)
        {
            var kind = e.IsVibrationPlate ? SessionRowKind.Vibration : e.IsCardio ? SessionRowKind.Cardio : SessionRowKind.Standard;
            // The same exercise name can exist on several machines/equipment types (e.g.
            // "Biceps Curl" on a Bowflex vs. as a Dumbbell exercise) — the machine label
            // disambiguates them in this flat dropdown, since a Picker can't group/nest.
            AvailableExercises.Add(new ExercisePickerOption(e.Id, e.Name, e.Equipment is ExerciseEquipment.BowflexMachine or ExerciseEquipment.Kettlebell, kind,
                EquipmentCatalog.MachineLabel(e, _rigs)));
        }
        AvailableExercises.Add(new ExercisePickerOption(AddCustomExerciseSentinelId, "+ Add custom exercise…", false, SessionRowKind.Standard, null));
        SelectedExerciseToAdd = AvailableExercises.FirstOrDefault(o => o.Id == previousSelection);
    }

    /// <summary>
    /// Called instead of LoadAsync when StandardBuilderPage reappears after
    /// pushing "exerciseEditor" for the picker's "+ Add custom exercise…" row
    /// (see StandardBuilderPage.xaml.cs) — LoadAsync would re-fetch and
    /// overwrite the in-progress routine (name, added exercises, schedule
    /// days, reminder), which is exactly what this flow must preserve. Only
    /// refreshes the exercise list and auto-selects the exercise the member
    /// just created; a no-op if nothing was actually created (e.g. the member
    /// cancelled, or this page reappeared for some other reason).
    /// </summary>
    public async Task RefreshAfterReturnAsync()
    {
        var pendingId = _pendingExerciseBridge.ConsumePendingExerciseId();
        if (pendingId is null) return;

        var account = _session.ActiveAccount;
        var member = _session.ActiveMember;
        if (account is null || member is null) return;

        _shared = await _repo.GetSharedLibraryAsync(account.Id);
        _allExercises = _shared.Exercises.Concat(_manufacturer.Exercises)
            .Where(e => e.Visibility == Visibility.Manufacturer || e.OwnerMemberId == member.Id || e.Visibility == Visibility.Account)
            .ToList();
        RebuildAvailableExercises();
        SelectedExerciseToAdd = AvailableExercises.FirstOrDefault(o => o.Id == pendingId);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _editingRoutineId = query.TryGetValue("routineId", out var id) ? Guid.Parse((string)id) : null;
        _occDate = query.TryGetValue("occDate", out var od) ? DateOnly.Parse((string)od) : null;
        _occTime = query.TryGetValue("occTime", out var ot) ? TimeOnly.ParseExact((string)ot, "HH:mm") : null;
        _fromHome = query.TryGetValue("fromHome", out var fh) && (string)fh == "true";
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
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
            _memberName = member.DisplayName;
            // Lets the Add Exercise/Browse Exercises/Add From Ritual/Schedule sub-pages
            // reach this same in-progress instance — see IActiveRoutineBuilderContext.
            _builderContext.ActiveStandard = this;

            _shared = await _repo.GetSharedLibraryAsync(account.Id);
            _manufacturer = await _repo.GetManufacturerLibraryAsync();
            _rigs = (await _repo.GetRigCatalogAsync()).Rigs;
            _allExercises = _shared.Exercises.Concat(_manufacturer.Exercises)
                .Where(e => e.Visibility == Visibility.Manufacturer || e.OwnerMemberId == _memberId || e.Visibility == Visibility.Account)
                .ToList();
            RebuildAvailableExercises();

            _memberData = await _repo.GetMemberDataAsync(account.Id, member.Id);
            // Assigned now (not at Save time) so the schedule/reminder editor and the
            // eventual routine share one id whether this is a fresh workout or an edit.
            _routineId = _editingRoutineId ?? Guid.NewGuid();
            Reminder.Load(_memberData, _routineId);

            if (_editingRoutineId is not Guid routineId) return;
            var routine = _shared.Routines.FirstOrDefault(r => r.Id == routineId);
            if (routine is null)
            {
                // Home lets a member schedule a manufacturer routine directly (see
                // HomeViewModel.AddWorkout), so "Edit" here can land on one. It can't
                // be mutated in place — Save() below forks it into a private copy
                // instead, the same way a mid-session "just this occurrence" edit does.
                routine = _manufacturer.Routines.FirstOrDefault(r => r.Id == routineId);
                if (routine is null) return;
                _isManufacturerFork = true;
            }

            RoutineName = routine.Name;
            IsAccountShared = routine.Visibility == Visibility.Account;
            // Replacing the whole collection (rather than Clear() + Add() in place) avoids
            // BindableLayout briefly seeing an empty source mid-rebuild — that transient
            // empty state crashes natively inside WinUI's own child-collection handling
            // (see the same fix in WorkoutsViewModel.Rebuild()).
            var exerciseRows = new ObservableCollection<StandardExerciseRowViewModel>();
            foreach (var target in routine.Exercises)
            {
                exerciseRows.Add(BuildRowFromTarget(target));
            }
            ExerciseRows = exerciseRows;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void AddExercise()
    {
        if (SelectedExerciseToAdd is not ExercisePickerOption option) return;
        var row = new StandardExerciseRowViewModel(option.Id, option.Name, option.HasWeight, option.Kind, MoveUpCommand, MoveDownCommand, RemoveRowCommand);
        switch (option.Kind)
        {
            case SessionRowKind.Standard:
                row.Groups.Add(new SetGroupRowViewModel(PendingSets, PendingReps, PendingWeight, row.RemoveGroupCommand));
                break;
            case SessionRowKind.Vibration:
                row.Stance = PendingStance;
                row.Mode = PendingMode;
                row.Duration = PendingDuration;
                row.Frequency = PendingFrequency;
                row.Level = PendingLevel;
                break;
            case SessionRowKind.Cardio:
                row.Time = PendingTime;
                row.Resistance = PendingResistance;
                break;
        }
        ExerciseRows.Add(row);
        SelectedExerciseToAdd = null;
    }

    /// <summary>Second way to add an exercise, alongside the Category/Exercise dropdown
    /// above — called from StandardBuilderPage's embedded exercise-library browser (see
    /// LibraryView.ExercisePickMode). No staged detail fields here since the library
    /// doesn't go through the dropdown's Add card; added blank like AddExercise used to
    /// before staging existed, still editable on the row afterward either way.</summary>
    public void AddExerciseFromLibrary(Guid exerciseId)
    {
        var exercise = _allExercises.FirstOrDefault(e => e.Id == exerciseId);
        if (exercise is null) return;
        var kind = exercise.IsVibrationPlate ? SessionRowKind.Vibration : exercise.IsCardio ? SessionRowKind.Cardio : SessionRowKind.Standard;
        var hasWeight = exercise.Equipment is ExerciseEquipment.BowflexMachine or ExerciseEquipment.Kettlebell;
        var row = new StandardExerciseRowViewModel(exercise.Id, exercise.Name, hasWeight, kind, MoveUpCommand, MoveDownCommand, RemoveRowCommand);
        if (kind == SessionRowKind.Standard)
        {
            row.Groups.Add(new SetGroupRowViewModel("", "", "", row.RemoveGroupCommand));
        }
        ExerciseRows.Add(row);
    }

    /// <summary>Third way to add an exercise — copying one over from another routine's
    /// own exercise list (see AddFromRitualViewModel). Shares the exact row-construction
    /// logic LoadAsync uses when opening an existing routine for edit, via BuildRowFromTarget,
    /// so a copied-in exercise looks and behaves identically to one loaded from a saved routine.</summary>
    public void AddExerciseFromTarget(RoutineExerciseTarget target) => ExerciseRows.Add(BuildRowFromTarget(target));

    private StandardExerciseRowViewModel BuildRowFromTarget(RoutineExerciseTarget target)
    {
        var option = AvailableExercises.FirstOrDefault(o => o.Id == target.ExerciseId);
        var kind = option?.Kind ?? SessionRowKind.Standard;
        var row = new StandardExerciseRowViewModel(target.ExerciseId, option?.Name ?? "Exercise", option?.HasWeight ?? false, kind,
            MoveUpCommand, MoveDownCommand, RemoveRowCommand);

        switch (kind)
        {
            case SessionRowKind.Vibration:
                row.Stance = target.Stance ?? "";
                row.Mode = target.Mode ?? "";
                row.Duration = target.DurationSeconds?.ToString() ?? "";
                row.Frequency = target.FrequencyHz?.ToString() ?? "";
                row.Level = target.Level?.ToString() ?? "";
                break;
            case SessionRowKind.Cardio:
                row.Time = target.TimeMinutes?.ToString() ?? "";
                row.Resistance = target.Resistance?.ToString() ?? "";
                break;
            default:
                foreach (var g in target.Groups)
                {
                    row.Groups.Add(new SetGroupRowViewModel(g.Sets, g.Reps, g.Weight, row.RemoveGroupCommand));
                }
                if (row.Groups.Count == 0) row.Groups.Add(new SetGroupRowViewModel("", "", "", row.RemoveGroupCommand));
                break;
        }
        return row;
    }

    [RelayCommand]
    private void MoveUp(StandardExerciseRowViewModel row)
    {
        var i = ExerciseRows.IndexOf(row);
        if (i > 0) ExerciseRows.Move(i, i - 1);
    }

    [RelayCommand]
    private void MoveDown(StandardExerciseRowViewModel row)
    {
        var i = ExerciseRows.IndexOf(row);
        if (i >= 0 && i < ExerciseRows.Count - 1) ExerciseRows.Move(i, i + 1);
    }

    [RelayCommand]
    private void RemoveRow(StandardExerciseRowViewModel row) => ExerciseRows.Remove(row);

    private const string DefaultRoutineName = "New Workout";

    [RelayCommand]
    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(RoutineName) || ExerciseRows.Count == 0) return;
        if (Shell.Current?.CurrentPage is not Page page) return;

        var now = DateTimeOffset.UtcNow;
        var existing = _editingRoutineId is Guid id ? _shared.Routines.FirstOrDefault(r => r.Id == id) : null;

        var trimmedName = RoutineName.Trim();
        if (string.Equals(trimmedName, DefaultRoutineName, StringComparison.OrdinalIgnoreCase))
        {
            await page.DisplayAlertAsync("Name it first",
                $"\"{DefaultRoutineName}\" is just the placeholder — give this workout a real name.", "OK");
            return;
        }

        // Vibration/Cardio authoring defaults are deliberately optional (the hint text
        // next to them says so — blank just means the member fills it in live each
        // time), so only Standard sets/reps/weight count as "missing" here.
        var incompleteRows = ExerciseRows.Where(r => r.Kind == SessionRowKind.Standard &&
            r.Groups.Any(g => string.IsNullOrWhiteSpace(g.Sets) || string.IsNullOrWhiteSpace(g.Reps) || (r.HasWeight && string.IsNullOrWhiteSpace(g.Weight))))
            .ToList();
        if (incompleteRows.Count > 0)
        {
            var names = string.Join(", ", incompleteRows.Select(r => r.Name));
            var saveAnyway = await page.DisplayAlertAsync("Missing details",
                $"These exercises are missing sets/reps or weight: {names}. Save anyway, or go back and fill them in?",
                "Save anyway", "Let me fix it");
            if (!saveAnyway)
            {
                if (page is StandardBuilderPage sbp) await sbp.ScrollToExerciseRow(ExerciseRows.IndexOf(incompleteRows[0]));
                return;
            }
        }

        // A "copy to mine" fork still displays under the source's own name until
        // an exercise is actually added or removed — see RoutineNamingHelper.
        // Once that happens it's promoted to a fully independent custom workout:
        // auto-renamed "[member]'s [title]" (unless already renamed away from
        // the source's name) and no longer exempted from the uniqueness check below.
        var currentKeys = RoutineNamingHelper.ExerciseKeysFor(RoutineType.Standard, ExerciseRows.Select(r => r.ExerciseId), null);
        var stillShadowingSource = existing?.SourceExerciseKeysSnapshot is not null;
        if (stillShadowingSource && RoutineNamingHelper.HasDivergedFromSource(existing!.SourceExerciseKeysSnapshot, currentKeys))
        {
            if (string.Equals(trimmedName, existing.Name, StringComparison.OrdinalIgnoreCase))
            {
                trimmedName = RoutineNamingHelper.ApplyOwnerPrefix(_memberName, trimmedName);
                RoutineName = trimmedName;
            }
            existing.SourceExerciseKeysSnapshot = null;
            stillShadowingSource = false;
        }

        // Excludes the routine actually being overwritten in place, but NOT a
        // manufacturer routine being forked (existing is null then) — forking
        // into a same-named private copy would leave two identically-named
        // routines visible to this member, exactly the confusion this guards against.
        // Also skipped while stillShadowingSource: matching the source's name on
        // an unmodified copy is the intended behavior, not a collision.
        var others = _shared.Routines.Concat(_manufacturer.Routines)
            .Where(r => existing is null || r.Id != existing.Id).ToList();
        if (!stillShadowingSource && others.Any(r => string.Equals(r.Name, trimmedName, StringComparison.OrdinalIgnoreCase)))
        {
            var suggestion = SuggestUniqueName(trimmedName, others);
            var useSuggestion = await page.DisplayAlertAsync("Name already used",
                $"A workout named \"{trimmedName}\" already exists. Use \"{suggestion}\" instead?",
                $"Use \"{suggestion}\"", "Let me rename it");
            if (!useSuggestion)
            {
                if (page is StandardBuilderPage sbp) sbp.FocusRoutineName();
                return;
            }
            RoutineName = suggestion;
            trimmedName = suggestion;
        }

        var justThisOccurrence = false;
        if (_occDate is not null && _occTime is not null)
        {
            justThisOccurrence = await page.DisplayAlertAsync("Save changes",
                "Apply this change to just this occurrence, or to every future occurrence of this workout?",
                "Just this occurrence", "All future occurrences");
        }

        if (justThisOccurrence && _occDate is DateOnly occDate && _occTime is TimeOnly occTime)
        {
            var fork = new RoutineDefinition
            {
                Id = Guid.NewGuid(),
                AccountId = _accountId,
                OwnerMemberId = _memberId,
                OwnerNameSnapshot = _memberName,
                SourceRoutineId = _editingRoutineId,
                SourceNameSnapshot = existing?.OwnerNameSnapshot is null ? existing?.Name : existing.OwnerDisplayName,
                Name = RoutineName.Trim(),
                Visibility = Visibility.Private,
                Type = RoutineType.Standard,
                Exercises = ExerciseRows.Select(BuildTarget).ToList(),
                UpdatedAt = now,
            };
            _shared.Routines.Add(fork);
            await _repo.SaveSharedLibraryAsync(_accountId, _shared);

            // Replace any earlier one-time fork of this same original routine
            // for this exact date/time, but leave unrelated one-time workouts alone.
            _memberData.Schedule.OneTimeOverrides.RemoveAll(o => o.Date == occDate && o.Time == occTime &&
                (o.RoutineId == _editingRoutineId || _shared.Routines.FirstOrDefault(r => r.Id == o.RoutineId)?.SourceRoutineId == _editingRoutineId));
            _memberData.Schedule.OneTimeOverrides.Add(new OneTimeAssignment { Date = occDate, Time = occTime, RoutineId = fork.Id });
            _memberData.Schedule.UpdatedAt = now;
            Reminder.ApplyTo(_memberData, fork.Id);
            await _repo.SaveMemberDataAsync(_accountId, _memberId, _memberData);
            await _reminders.RescheduleAllAsync(_memberData, _accountId, _memberId, FindRoutineName);

            _builderContext.ActiveStandard = null;
            await Shell.Current!.GoToAsync("//home");
            return;
        }

        // Same shadow-the-source naming rule as the Copy to Mine path above, applied
        // to the other way a manufacturer routine gets forked: editing it directly
        // from Home. Since this fork doesn't exist yet, there's no `existing` to
        // carry a snapshot — the manufacturer original itself is the baseline.
        Guid? manufacturerForkSourceId = null;
        string? manufacturerForkSourceName = null;
        List<string>? manufacturerForkSourceKeys = null;

        if (_isManufacturerFork)
        {
            // Move this member's own RoutineSchedule entry (if any) from the
            // manufacturer routine's id to a fresh private id — Reminder.ApplyTo
            // below writes it back under _routineId per the (unchanged) editor
            // state, so this only needs to strip the OLD id's entry out from under
            // it first. (The just-this-occurrence branch above already forks
            // independently and never reaches here, so this can't double up with it.)
            var oldId = _routineId;
            var manufacturerSource = _manufacturer.Routines.FirstOrDefault(r => r.Id == oldId);
            if (manufacturerSource is not null)
            {
                var sourceKeys = RoutineNamingHelper.ExerciseKeysFor(RoutineType.Standard, manufacturerSource.Exercises.Select(e => e.ExerciseId), null);
                if (RoutineNamingHelper.HasDivergedFromSource(sourceKeys, currentKeys))
                {
                    if (string.Equals(trimmedName, manufacturerSource.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        trimmedName = RoutineNamingHelper.ApplyOwnerPrefix(_memberName, trimmedName);
                        RoutineName = trimmedName;
                    }
                }
                else
                {
                    manufacturerForkSourceId = manufacturerSource.Id;
                    manufacturerForkSourceName = manufacturerSource.OwnerNameSnapshot is null ? manufacturerSource.Name : manufacturerSource.OwnerDisplayName;
                    manufacturerForkSourceKeys = sourceKeys;
                }
            }

            _routineId = Guid.NewGuid();
            _memberData.RoutineSchedules.Remove(oldId);
            foreach (var oneTime in _memberData.Schedule.OneTimeOverrides.Where(o => o.RoutineId == oldId))
                oneTime.RoutineId = _routineId;
        }

        var routine = existing ?? new RoutineDefinition
        {
            Id = _routineId, AccountId = _accountId, OwnerMemberId = _memberId, OwnerNameSnapshot = _memberName,
            SourceRoutineId = manufacturerForkSourceId, SourceNameSnapshot = manufacturerForkSourceName, SourceExerciseKeysSnapshot = manufacturerForkSourceKeys,
        };
        routine.Name = RoutineName.Trim();
        routine.Visibility = IsAccountShared ? Visibility.Account : Visibility.Private;
        routine.Type = RoutineType.Standard;
        routine.Exercises = ExerciseRows.Select(BuildTarget).ToList();
        routine.UpdatedAt = now;

        var isNew = existing is null;
        if (isNew) _shared.Routines.Add(routine);
        await _repo.SaveSharedLibraryAsync(_accountId, _shared);

        Reminder.ApplyTo(_memberData, _routineId);
        await _repo.SaveMemberDataAsync(_accountId, _memberId, _memberData);
        await _reminders.RescheduleAllAsync(_memberData, _accountId, _memberId, FindRoutineName);

        // Lets HomeViewModel offer "add this to today's schedule?" once we're back
        // there — see IHomeWorkoutBridge. Only for a genuinely new routine: editing
        // an existing one from Home (e.g. via a TodaySlot's Edit button) is already
        // on the schedule, nothing to offer.
        if (_fromHome && isNew) _homeWorkoutBridge.SetPendingRoutineId(routine.Id);

        _builderContext.ActiveStandard = null;
        await Shell.Current!.GoToAsync(_occDate is not null ? "//home" : "..");
    }

    private string? FindRoutineName(Guid routineId) =>
        _shared.Routines.FirstOrDefault(r => r.Id == routineId)?.Name
        ?? _manufacturer.Routines.FirstOrDefault(r => r.Id == routineId)?.Name;

    /// <summary>Appends "(1)", "(2)", ... to baseName — tries each until one isn't already taken.</summary>
    private static string SuggestUniqueName(string baseName, IReadOnlyCollection<RoutineDefinition> others)
    {
        var taken = others.Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i <= 999; i++)
        {
            var numbered = $"{baseName}({i})";
            if (!taken.Contains(numbered)) return numbered;
        }
        return $"{baseName} ({Guid.NewGuid().ToString()[..4]})";
    }

    private static RoutineExerciseTarget BuildTarget(StandardExerciseRowViewModel row) => row.Kind switch
    {
        SessionRowKind.Vibration => new RoutineExerciseTarget
        {
            ExerciseId = row.ExerciseId,
            Stance = row.Stance, Mode = row.Mode,
            DurationSeconds = ParseIntOrNull(row.Duration), FrequencyHz = ParseIntOrNull(row.Frequency), Level = ParseIntOrNull(row.Level),
        },
        SessionRowKind.Cardio => new RoutineExerciseTarget
        {
            ExerciseId = row.ExerciseId,
            TimeMinutes = ParseIntOrNull(row.Time), Resistance = ParseIntOrNull(row.Resistance),
        },
        _ => new RoutineExerciseTarget
        {
            ExerciseId = row.ExerciseId,
            Groups = row.Groups.Select(g => new SetGroupTarget { Sets = g.Sets, Reps = g.Reps, Weight = g.Weight }).ToList(),
        },
    };

    private static int? ParseIntOrNull(string value) => int.TryParse(value, out var i) ? i : null;

    [RelayCommand]
    private async Task Cancel()
    {
        _builderContext.ActiveStandard = null;
        await Shell.Current.GoToAsync("..");
    }
}

public record ExercisePickerOption(Guid Id, string Name, bool HasWeight, SessionRowKind Kind, string? MachineLabel)
{
    public override string ToString() => MachineLabel is null ? Name : $"{Name} — {MachineLabel}";
}

public partial class StandardExerciseRowViewModel : ObservableObject
{
    public Guid ExerciseId { get; }
    public string Name { get; }
    public bool HasWeight { get; }
    public SessionRowKind Kind { get; }
    public bool IsStandard => Kind == SessionRowKind.Standard;
    public bool IsVibration => Kind == SessionRowKind.Vibration;
    public bool IsCardio => Kind == SessionRowKind.Cardio;
    public string[] StanceOptions => VibrationOptions.Stances;
    public string[] ModeOptions => VibrationOptions.Modes;

    public ObservableCollection<SetGroupRowViewModel> Groups { get; } = new();
    public IRelayCommand<StandardExerciseRowViewModel> MoveUpCommand { get; }
    public IRelayCommand<StandardExerciseRowViewModel> MoveDownCommand { get; }
    public IRelayCommand<StandardExerciseRowViewModel> RemoveCommand { get; }

    [ObservableProperty] public partial string Stance { get; set; } = "";
    [ObservableProperty] public partial string Mode { get; set; } = "";
    [ObservableProperty] public partial string Duration { get; set; } = "";
    [ObservableProperty] public partial string Frequency { get; set; } = "";
    [ObservableProperty] public partial string Level { get; set; } = "";
    [ObservableProperty] public partial string Time { get; set; } = "";
    [ObservableProperty] public partial string Resistance { get; set; } = "";

    public StandardExerciseRowViewModel(Guid exerciseId, string name, bool hasWeight, SessionRowKind kind,
        IRelayCommand<StandardExerciseRowViewModel> moveUp, IRelayCommand<StandardExerciseRowViewModel> moveDown, IRelayCommand<StandardExerciseRowViewModel> remove)
    {
        ExerciseId = exerciseId;
        Name = name;
        HasWeight = hasWeight;
        Kind = kind;
        MoveUpCommand = moveUp;
        MoveDownCommand = moveDown;
        RemoveCommand = remove;
    }

    [RelayCommand]
    private void AddGroup() => Groups.Add(new SetGroupRowViewModel("", "", "", RemoveGroupCommand));

    [RelayCommand]
    private void RemoveGroup(SetGroupRowViewModel group)
    {
        if (Groups.Count > 1) Groups.Remove(group);
    }
}

public partial class SetGroupRowViewModel : ObservableObject
{
    [ObservableProperty] public partial string Sets { get; set; }
    [ObservableProperty] public partial string Reps { get; set; }
    [ObservableProperty] public partial string Weight { get; set; }
    public IRelayCommand<SetGroupRowViewModel> RemoveCommand { get; }

    public SetGroupRowViewModel(string sets, string reps, string weight, IRelayCommand<SetGroupRowViewModel> removeCommand)
    {
        Sets = sets;
        Reps = reps;
        Weight = weight;
        RemoveCommand = removeCommand;
    }
}
