using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// The session runner: creates or resumes one member's WorkoutSession for a
/// routine/date/time. This never reads or writes RoutineDefinition — that's
/// the whole point of the definition/instance split. Pre-fill for a fresh
/// session comes from this member's own most recent WorkoutSession for the
/// same routine, never from another member's log and never from the routine
/// definition itself.
/// </summary>
public partial class SessionViewModel : ObservableObject, IQueryAttributable, IDisposable
{
    private const int DefaultRestSeconds = 60;

    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;
    private readonly IHiitSoundService _sounds;

    private Guid _routineId;
    private TimeOnly _time;
    private DateOnly _date;
    private DateOnly? _browseDate;
    private Guid _accountId;
    private Guid _memberId;
    private MemberData _memberData = new();
    private WorkoutSession? _existingSession;
    private IDispatcherTimer? _restTimer;

    [ObservableProperty] public partial string RoutineName { get; set; } = "";
    [ObservableProperty] public partial ObservableCollection<SessionExerciseRowViewModel> Rows { get; set; } = new();
    [ObservableProperty] public partial bool IsResolving { get; set; }
    [ObservableProperty] public partial ObservableCollection<ResolveItemViewModel> ResolveItems { get; set; } = new();

    [ObservableProperty] public partial bool IsResting { get; set; }
    [ObservableProperty] public partial int RestRemainingSeconds { get; set; }

    [ObservableProperty] public partial bool ShowPrCelebration { get; set; }
    [ObservableProperty] public partial ObservableCollection<string> NewPersonalRecords { get; set; } = new();

    public Guid RoutineId => _routineId;
    public TimeOnly Time => _time;
    public DateOnly Date => _date;

    public SessionViewModel(IActiveSessionService session, IWorkoutRepository repo, IHiitSoundService sounds)
    {
        _session = session;
        _repo = repo;
        _sounds = sounds;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _routineId = Guid.Parse((string)query["routineId"]);
        _time = TimeOnly.ParseExact((string)query["time"], "HH:mm");
        _date = query.TryGetValue("date", out var d) ? DateOnly.Parse((string)d) : DateOnly.FromDateTime(DateTime.Today);
        // Set only when this session was started via Home's "Browse Workouts" on a day
        // other than today — see IHomeBrowseContext/WorkoutsView.OnRoutineTapped. Asked
        // about at Finish time, not before, so starting the workout isn't held up by it.
        _browseDate = query.TryGetValue("browseDate", out var bd) ? DateOnly.Parse((string)bd) : null;
    }

    public async Task LoadAsync()
    {
        var account = _session.ActiveAccount;
        var member = _session.ActiveMember;
        if (account is null || member is null) return;
        _accountId = account.Id;
        _memberId = member.Id;

        var shared = await _repo.GetSharedLibraryAsync(account.Id);
        var manufacturer = await _repo.GetManufacturerLibraryAsync();
        var routine = shared.Routines.FirstOrDefault(r => r.Id == _routineId)
            ?? manufacturer.Routines.FirstOrDefault(r => r.Id == _routineId);
        if (routine is null) return;
        RoutineName = routine.Name;

        Exercise? FindExercise(Guid id) =>
            shared.Exercises.FirstOrDefault(e => e.Id == id) ?? manufacturer.Exercises.FirstOrDefault(e => e.Id == id);

        _memberData = await _repo.GetMemberDataAsync(account.Id, member.Id);

        _existingSession = _memberData.Sessions.FirstOrDefault(s =>
            s.RoutineDefinitionId == _routineId && s.Date == _date && s.Time == _time);

        // Pre-fill source: this member's own latest session for this routine —
        // never the routine definition, and never another member's session.
        var lastOwnSession = _existingSession ?? _memberData.Sessions
            .Where(s => s.RoutineDefinitionId == _routineId)
            .OrderByDescending(s => s.Date)
            .FirstOrDefault();

        // Replacing the whole collection (rather than Clear() + Add() in place) avoids
        // BindableLayout briefly seeing an empty source mid-rebuild — that transient
        // empty state crashes natively inside WinUI's own child-collection handling
        // (see the same fix in WorkoutsViewModel.Rebuild()).
        var rows = new ObservableCollection<SessionExerciseRowViewModel>();
        foreach (var target in routine.Exercises)
        {
            var exercise = FindExercise(target.ExerciseId);
            var priorEntry = lastOwnSession?.Entries.FirstOrDefault(e => e.ExerciseId == target.ExerciseId);

            if (exercise?.IsVibrationPlate == true)
            {
                rows.Add(BuildVibrationRow(target, exercise, priorEntry));
            }
            else if (exercise?.IsCardio == true)
            {
                rows.Add(BuildCardioRow(target, exercise, priorEntry));
            }
            else
            {
                rows.Add(BuildStandardRow(target, exercise, priorEntry));
            }
        }
        Rows = rows;
    }

    private SessionExerciseRowViewModel BuildStandardRow(RoutineExerciseTarget target, Exercise? exercise, SessionExerciseEntry? priorEntry)
    {
        var hasWeight = exercise?.Equipment is ExerciseEquipment.BowflexMachine or ExerciseEquipment.Kettlebell;
        var row = new SessionExerciseRowViewModel(target.ExerciseId, exercise?.Name ?? "Exercise", hasWeight, SessionRowKind.Standard, StartRestCommand);

        var sourceGroups = priorEntry?.Groups.Count > 0
            ? priorEntry.Groups.Select(g => (g.Sets, g.Reps, g.Weight))
            : target.Groups.Select(g => (g.Sets, g.Reps, hasWeight ? g.Weight : ""));

        foreach (var (sets, reps, weight) in sourceGroups)
        {
            row.Groups.Add(new SessionGroupRowViewModel(sets, reps, weight));
        }
        if (row.Groups.Count == 0) row.Groups.Add(new SessionGroupRowViewModel("", "", ""));
        return row;
    }

    private SessionExerciseRowViewModel BuildVibrationRow(RoutineExerciseTarget target, Exercise? exercise, SessionExerciseEntry? priorEntry)
    {
        var row = new SessionExerciseRowViewModel(target.ExerciseId, exercise?.Name ?? "Exercise", false, SessionRowKind.Vibration, StartRestCommand)
        {
            Stance = priorEntry?.Vibration?.Stance ?? target.Stance ?? "",
            Mode = priorEntry?.Vibration?.Mode ?? target.Mode ?? "",
            Duration = (priorEntry?.Vibration?.DurationSeconds ?? target.DurationSeconds)?.ToString() ?? "",
            Frequency = (priorEntry?.Vibration?.FrequencyHz ?? target.FrequencyHz)?.ToString() ?? "",
            Level = (priorEntry?.Vibration?.Level ?? target.Level)?.ToString() ?? "",
        };
        return row;
    }

    private SessionExerciseRowViewModel BuildCardioRow(RoutineExerciseTarget target, Exercise? exercise, SessionExerciseEntry? priorEntry)
    {
        var row = new SessionExerciseRowViewModel(target.ExerciseId, exercise?.Name ?? "Exercise", false, SessionRowKind.Cardio, StartRestCommand)
        {
            Time = (priorEntry?.CardioTimeMinutes ?? target.TimeMinutes)?.ToString() ?? "",
            Resistance = (priorEntry?.CardioResistance ?? target.Resistance)?.ToString() ?? "",
            // Distance is never routine-authored, only ever entered live or carried from a prior log.
            Distance = priorEntry?.CardioDistance?.ToString() ?? "",
        };
        return row;
    }

    /// <summary>
    /// Lists every standard set group still missing sets, reps, or (where
    /// relevant) weight — matching the design's Finish Workout modal — before
    /// saving. Vibration/Cardio rows have no "set" concept to resolve, so
    /// they're saved as entered, blank fields included.
    /// </summary>
    [RelayCommand]
    private async Task Finish()
    {
        if (!await ResolveBrowseDateAsync()) return;

        var pending = new List<ResolveItemViewModel>();
        foreach (var row in Rows.Where(r => r.Kind == SessionRowKind.Standard && r.IsDone))
        {
            for (var i = 0; i < row.Groups.Count; i++)
            {
                var group = row.Groups[i];
                var missingCore = string.IsNullOrEmpty(group.Sets) || string.IsNullOrEmpty(group.Reps);
                var missingWeight = row.HasWeight && string.IsNullOrEmpty(group.Weight);
                if (!missingCore && !missingWeight) continue;

                var label = row.Groups.Count > 1 ? $"{row.Name} — group {i + 1}" : row.Name;
                pending.Add(new ResolveItemViewModel(label, row.HasWeight, group));
            }
        }

        if (pending.Count > 0)
        {
            // Replacing the whole collection (rather than Clear() + Add() in place) avoids
            // BindableLayout briefly seeing an empty source mid-rebuild — that transient
            // empty state crashes natively inside WinUI's own child-collection handling
            // (see the same fix in WorkoutsViewModel.Rebuild()).
            ResolveItems = new ObservableCollection<ResolveItemViewModel>(pending);
            IsResolving = true;
            return;
        }

        await CompleteFinishAsync();
    }

    /// <summary>Asks which day this run should count for, only once, right when the
    /// member actually finishes — not when they started browsing/picked the routine.
    /// Returns false if they backed out of the prompt entirely (Finish should abort).</summary>
    private async Task<bool> ResolveBrowseDateAsync()
    {
        if (_browseDate is not DateOnly browseDate) return true;
        _browseDate = null; // ask at most once per Finish flow, even if Finish is retried after Cancel Resolve

        if (Shell.Current?.CurrentPage is not Page page) return true;
        var browseDateLabel = browseDate.ToDateTime(TimeOnly.MinValue).ToString("MMM d");
        var saveToBrowseDate = $"Save to {browseDateLabel}";
        var choice = await page.DisplayActionSheetAsync(
            "Save this workout to the day you were browsing from, or to today?",
            "Cancel", null, saveToBrowseDate, "Save to today");
        if (choice is null || choice == "Cancel") return false;
        if (choice == saveToBrowseDate) _date = browseDate;
        return true;
    }

    [RelayCommand]
    private async Task ConfirmResolve()
    {
        IsResolving = false;
        await CompleteFinishAsync();
    }

    [RelayCommand]
    private void CancelResolve() => IsResolving = false;

    private async Task CompleteFinishAsync()
    {
        var entries = Rows.Select(BuildEntry).ToList();
        var records = ComputePersonalRecords(entries);

        var now = DateTimeOffset.UtcNow;

        if (_existingSession is not null)
        {
            _existingSession.Entries = entries;
            _existingSession.Status = SessionStatus.Completed;
            _existingSession.CompletedAt = now;
            _existingSession.UpdatedAt = now;
        }
        else
        {
            _memberData.Sessions.Add(new WorkoutSession
            {
                Id = Guid.NewGuid(),
                AccountId = _accountId,
                MemberId = _memberId,
                RoutineDefinitionId = _routineId,
                RoutineNameSnapshot = RoutineName,
                Date = _date,
                Time = _time,
                Status = SessionStatus.Completed,
                CompletedAt = now,
                UpdatedAt = now,
                Entries = entries,
            });
        }

        // Note what this does NOT do: it never touches RoutineDefinition or
        // SharedLibrary. Only this member's own MemberData document changes.
        await _repo.SaveMemberDataAsync(_accountId, _memberId, _memberData);

        if (records.Count > 0)
        {
            NewPersonalRecords = new ObservableCollection<string>(records);
            ShowPrCelebration = true;
            return;
        }
        await Shell.Current.GoToAsync("..");
    }

    private static SessionExerciseEntry BuildEntry(SessionExerciseRowViewModel row) => row.Kind switch
    {
        SessionRowKind.Vibration => new SessionExerciseEntry
        {
            ExerciseId = row.ExerciseId,
            Done = row.IsDone,
            Vibration = new VibrationSettings
            {
                Stance = row.Stance, Mode = row.Mode,
                DurationSeconds = ParseIntOrNull(row.Duration),
                FrequencyHz = ParseIntOrNull(row.Frequency),
                Level = ParseIntOrNull(row.Level),
            },
        },
        SessionRowKind.Cardio => new SessionExerciseEntry
        {
            ExerciseId = row.ExerciseId,
            Done = row.IsDone,
            CardioTimeMinutes = ParseIntOrNull(row.Time),
            CardioResistance = ParseIntOrNull(row.Resistance),
            CardioDistance = double.TryParse(row.Distance, out var d) ? d : null,
        },
        _ => new SessionExerciseEntry
        {
            ExerciseId = row.ExerciseId,
            Done = row.IsDone,
            Groups = row.Groups.Select(g => new SessionSetEntry { Sets = g.Sets, Reps = g.Reps, Weight = g.Weight }).ToList(),
        },
    };

    private static int? ParseIntOrNull(string value) => int.TryParse(value, out var i) ? i : null;

    [RelayCommand]
    private async Task Exit() => await Shell.Current.GoToAsync("..");

    /// <summary>
    /// Opens the builder for this routine mid-session. The builder itself asks,
    /// on Save, whether the change should apply to just this occurrence (forks
    /// a private copy and one-time-assigns it to this date/time) or to every
    /// future occurrence (edits the shared routine normally) — see
    /// StandardBuilderViewModel.Save.
    /// </summary>
    [RelayCommand]
    private async Task EditWorkout() =>
        await Shell.Current.GoToAsync($"standardBuilder?routineId={_routineId}&occDate={_date:yyyy-MM-dd}&occTime={_time:HH\\:mm}");

    /// <summary>Manual "I just finished a set" convenience — starts a countdown so the next set doesn't start too soon.</summary>
    [RelayCommand]
    private void StartRest()
    {
        _restTimer?.Stop();
        RestRemainingSeconds = DefaultRestSeconds;
        IsResting = true;
        _restTimer = Application.Current!.Dispatcher.CreateTimer();
        _restTimer.Interval = TimeSpan.FromSeconds(1);
        _restTimer.Tick += (_, _) => RestTick();
        _restTimer.Start();
    }

    private void RestTick()
    {
        RestRemainingSeconds--;
        if (RestRemainingSeconds > 0) return;
        _restTimer?.Stop();
        IsResting = false;
        _sounds.PlayBeep(false);
    }

    [RelayCommand]
    private void AddRestTime()
    {
        if (IsResting) RestRemainingSeconds += 15;
    }

    [RelayCommand]
    private void SubtractRestTime()
    {
        if (IsResting) RestRemainingSeconds = Math.Max(0, RestRemainingSeconds - 15);
    }

    [RelayCommand]
    private void SkipRest()
    {
        _restTimer?.Stop();
        IsResting = false;
    }

    /// <summary>
    /// Compares each newly-logged Standard exercise's heaviest set this
    /// session against every prior completed session's heaviest set for that
    /// same exercise — evaluated before the new session is added, so it never
    /// compares against itself.
    /// </summary>
    private List<string> ComputePersonalRecords(List<SessionExerciseEntry> newEntries)
    {
        var priorSessions = _memberData.Sessions.Where(s => s.Status == SessionStatus.Completed && s.Id != _existingSession?.Id).ToList();
        var records = new List<string>();

        foreach (var entry in newEntries.Where(e => e.Done && e.Vibration is null
                     && e.CardioTimeMinutes is null && e.CardioDistance is null && e.CardioResistance is null && e.Groups.Count > 0))
        {
            var newMax = entry.Groups.Select(g => ParseWeight(g.Weight)).DefaultIfEmpty(0).Max();
            if (newMax <= 0) continue;

            var priorMax = priorSessions
                .SelectMany(s => s.Entries.Where(e2 => e2.Done && e2.ExerciseId == entry.ExerciseId))
                .SelectMany(e2 => e2.Groups)
                .Select(g => ParseWeight(g.Weight))
                .DefaultIfEmpty(0)
                .Max();

            if (newMax <= priorMax) continue;
            var name = Rows.FirstOrDefault(r => r.ExerciseId == entry.ExerciseId)?.Name ?? "Exercise";
            records.Add($"{name}: {newMax:0.#} {_memberData.WeightUnit}");
        }
        return records;
    }

    private static double ParseWeight(string value) => double.TryParse(value, out var w) ? w : 0;

    [RelayCommand]
    private async Task DismissPrCelebration()
    {
        ShowPrCelebration = false;
        await Shell.Current.GoToAsync("..");
    }

    public void Dispose() => _restTimer?.Stop();
}

public enum SessionRowKind { Standard, Vibration, Cardio }

public partial class SessionExerciseRowViewModel : ObservableObject
{
    public Guid ExerciseId { get; }
    public string Name { get; }
    public bool HasWeight { get; }
    public SessionRowKind Kind { get; }
    public bool IsStandard => Kind == SessionRowKind.Standard;
    public bool IsVibration => Kind == SessionRowKind.Vibration;
    public bool IsCardio => Kind == SessionRowKind.Cardio;
    public ObservableCollection<SessionGroupRowViewModel> Groups { get; } = new();
    public IRelayCommand StartRestCommand { get; }

    public string[] StanceOptions => VibrationOptions.Stances;
    public string[] ModeOptions => VibrationOptions.Modes;

    /// <summary>Defaults to completed; unchecking it means this exercise isn't logged once the workout finishes.</summary>
    [ObservableProperty] public partial bool IsDone { get; set; } = true;

    [ObservableProperty] public partial string Stance { get; set; } = "";
    [ObservableProperty] public partial string Mode { get; set; } = "";
    [ObservableProperty] public partial string Duration { get; set; } = "";
    [ObservableProperty] public partial string Frequency { get; set; } = "";
    [ObservableProperty] public partial string Level { get; set; } = "";

    [ObservableProperty] public partial string Time { get; set; } = "";
    [ObservableProperty] public partial string Distance { get; set; } = "";
    [ObservableProperty] public partial string Resistance { get; set; } = "";

    public SessionExerciseRowViewModel(Guid exerciseId, string name, bool hasWeight, SessionRowKind kind, IRelayCommand startRestCommand)
    {
        ExerciseId = exerciseId;
        Name = name;
        HasWeight = hasWeight;
        Kind = kind;
        StartRestCommand = startRestCommand;
    }

    [RelayCommand]
    private void AddSet() => Groups.Add(new SessionGroupRowViewModel("", "", ""));

    [RelayCommand]
    private void ToggleDone() => IsDone = !IsDone;
}

public partial class SessionGroupRowViewModel : ObservableObject
{
    [ObservableProperty] public partial string Sets { get; set; }
    [ObservableProperty] public partial string Reps { get; set; }
    [ObservableProperty] public partial string Weight { get; set; }

    public SessionGroupRowViewModel(string sets, string reps, string weight)
    {
        Sets = sets;
        Reps = reps;
        Weight = weight;
    }
}

/// <summary>One row in the Finish Workout resolve list — Group is the same instance bound in the main exercise rows, so edits here are edits there.</summary>
public class ResolveItemViewModel
{
    public string Label { get; }
    public bool HasWeight { get; }
    public SessionGroupRowViewModel Group { get; }

    public ResolveItemViewModel(string label, bool hasWeight, SessionGroupRowViewModel group)
    {
        Label = label;
        HasWeight = hasWeight;
        Group = group;
    }
}
