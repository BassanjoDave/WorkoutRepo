using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;
using Visibility = WorkoutTracker.Models.Visibility;

namespace WorkoutTracker.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;
    private readonly ISyncStatusService _syncStatus;
    private readonly IWorkoutReminderService _reminders;
    private readonly IHomeWorkoutBridge _homeWorkoutBridge;
    private readonly IEntitlementService _entitlements;

    private Guid _accountId;
    private Guid _memberId;
    private MemberData _memberData = new();
    private SharedLibrary _shared = new();
    private ManufacturerLibrary _manufacturer = new();
    private DateOnly _today;

    [ObservableProperty] public partial ObservableCollection<WeekDayCellViewModel> WeekStrip { get; set; } = new();
    [ObservableProperty] public partial bool IsRestDay { get; set; }
    [ObservableProperty] public partial bool IsExerciseEnabled { get; set; } = true;

    /// <summary>Enabled dashboard modules (everything except "exercise", which is Home's permanent anchor), in the member's chosen order. HomePage's code-behind reads this to arrange its pre-built summary views.</summary>
    public List<string> EnabledModuleIds { get; private set; } = new();
    [ObservableProperty] public partial ObservableCollection<TodaySlotViewModel> TodaySlots { get; set; } = new();
    [ObservableProperty] public partial string SyncLabel { get; set; } = "";
    [ObservableProperty] public partial DateOnly SelectedDate { get; set; }
    [ObservableProperty] public partial string SelectedDateLabel { get; set; } = "";
    [ObservableProperty] public partial bool IsSelectedToday { get; set; } = true;

    public HomeViewModel(IActiveSessionService session, IWorkoutRepository repo, ISyncStatusService syncStatus,
        IWorkoutReminderService reminders, IHomeWorkoutBridge homeWorkoutBridge, IEntitlementService entitlements)
    {
        _session = session;
        _repo = repo;
        _syncStatus = syncStatus;
        _reminders = reminders;
        _homeWorkoutBridge = homeWorkoutBridge;
        _entitlements = entitlements;
        _syncStatus.Changed += () => MainThread.BeginInvokeOnMainThread(UpdateSyncLabel);
        UpdateSyncLabel();
    }

    /// <summary>Whether the active member can see the real card for this module —
    /// ungated modules (history, etc.) always return true. HomePage.xaml.cs uses
    /// this to decide between the real summary card and LockedModuleCardView.</summary>
    public bool HasPageAccess(string moduleId)
    {
        var account = _session.ActiveAccount;
        var member = _session.ActiveMember;
        if (account is null || member is null) return false;
        return moduleId switch
        {
            "nutrition" => _entitlements.HasPageAccess(account, member.Id, PageEntitlements.Nutrition),
            "stacks" => _entitlements.HasPageAccess(account, member.Id, PageEntitlements.Stacks),
            "measurements" => _entitlements.HasPageAccess(account, member.Id, PageEntitlements.Measurements),
            _ => true,
        };
    }

    private void UpdateSyncLabel()
    {
        SyncLabel = _syncStatus.State switch
        {
            SyncState.Syncing => "Syncing…",
            SyncState.Offline => _syncStatus.PendingCount > 0 ? $"Offline · {_syncStatus.PendingCount} pending" : "Offline",
            _ => _syncStatus.PendingCount > 0 ? $"{_syncStatus.PendingCount} pending" : "Synced",
        };
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
        _shared = await _repo.GetSharedLibraryAsync(account.Id);
        _manufacturer = await _repo.GetManufacturerLibraryAsync();

        LoadModuleConfig();

        var completedDates = _memberData.Sessions
            .Where(s => s.Status == SessionStatus.Completed)
            .Select(s => s.Date)
            .ToHashSet();

        _today = DateOnly.FromDateTime(DateTime.Today);
        var startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);

        // Replacing the whole collection (rather than Clear() + Add() in place) avoids
        // BindableLayout briefly seeing an empty source mid-rebuild — that transient
        // empty state crashes natively inside WinUI's own child-collection handling
        // (see the same fix in WorkoutsViewModel.Rebuild()).
        var weekStrip = new ObservableCollection<WeekDayCellViewModel>();
        for (var i = 0; i < 7; i++)
        {
            var day = startOfWeek.AddDays(i);
            var dateOnly = DateOnly.FromDateTime(day);
            var hasWorkout = ScheduleResolver.HasAnyWorkout(_memberData, dateOnly);
            var logged = completedDates.Contains(dateOnly);
            var isToday = dateOnly == _today;
            weekStrip.Add(new WeekDayCellViewModel(dateOnly, day.ToString("ddd")[..1].ToUpperInvariant(), isToday, hasWorkout, logged, SelectDayCommand));
        }
        WeekStrip = weekStrip;

        SelectDay(_today);

        // Fire-and-forget: keeps device-side reminders in sync with whatever
        // the schedule looks like right now, without threading the reminder
        // service through every place that can change the schedule.
        _ = _reminders.RescheduleAllAsync(_memberData, _accountId, _memberId, id =>
            _shared.Routines.FirstOrDefault(r => r.Id == id)?.Name ?? _manufacturer.Routines.FirstOrDefault(r => r.Id == id)?.Name);

        // Reappearing after "+ Add Workout" → "Create a new workout" sent the member
        // to build one — see IHomeWorkoutBridge. Offer to put it on today's schedule
        // right away instead of making them repeat the same "+ Add Workout" trip.
        if (_homeWorkoutBridge.ConsumePendingRoutineId() is Guid newRoutineId)
        {
            var newRoutine = _shared.Routines.FirstOrDefault(r => r.Id == newRoutineId)
                ?? _manufacturer.Routines.FirstOrDefault(r => r.Id == newRoutineId);
            // If the routine was already given a recurring schedule in its own
            // builder (the "Schedule & Reminder" toggle), it's already on the
            // calendar — asking "add to today?" and then "one-time or recurring?"
            // would just re-litigate a decision the member already made.
            if (newRoutine is not null && !_memberData.RoutineSchedules.ContainsKey(newRoutine.Id)
                && Shell.Current?.CurrentPage is Page page)
            {
                var add = await page.DisplayAlertAsync("Workout created",
                    $"Add \"{newRoutine.Name}\" to today's schedule?", "Add", "Not now");
                if (add) await AssignRoutineToScheduleAsync(page, newRoutine);
            }
        }
    }

    /// <summary>Reads the member's saved dashboard config, or synthesizes "everything enabled, in HomeModules.All order" if they've never customized it.</summary>
    private void LoadModuleConfig()
    {
        var exerciseConfig = _memberData.HomeModules.FirstOrDefault(m => m.ModuleId == "exercise");
        IsExerciseEnabled = exerciseConfig?.Enabled ?? true;

        if (_memberData.HomeModules.Count == 0)
        {
            EnabledModuleIds = HomeModules.All.Select(m => m.Id).ToList();
            return;
        }

        EnabledModuleIds = _memberData.HomeModules
            .Where(m => m.Enabled && m.ModuleId != "exercise")
            .OrderBy(m => m.Order)
            .Select(m => m.ModuleId)
            .ToList();
    }

    [RelayCommand]
    private async Task OpenMyRigs() => await Shell.Current.GoToAsync("myRigs");

    [RelayCommand]
    private void GoToToday() => SelectDay(_today);

    /// <summary>Manual override for a scheduled workout's completed state — lets a
    /// member un-mark one that auto-completed (or mark one done without actually
    /// running the player). "Not completed" has no session record at all elsewhere
    /// in this model, so unchecking removes the session rather than setting some
    /// third status; checking creates a bare Completed one with no logged entries,
    /// since there's no played data to attach.</summary>
    [RelayCommand]
    private async Task ToggleCompleted(TodaySlotViewModel slot)
    {
        var existingSession = _memberData.Sessions.FirstOrDefault(s =>
            s.RoutineDefinitionId == slot.RoutineId && s.Date == slot.Date && s.Time == slot.Time);

        if (existingSession is { Status: SessionStatus.Completed })
        {
            _memberData.Sessions.Remove(existingSession);
            slot.IsCompleted = false;
            slot.StartLabel = "Start";
        }
        else
        {
            if (existingSession is not null) _memberData.Sessions.Remove(existingSession);
            _memberData.Sessions.Add(new WorkoutSession
            {
                Id = Guid.NewGuid(),
                AccountId = _accountId,
                MemberId = _memberId,
                RoutineDefinitionId = slot.RoutineId,
                RoutineNameSnapshot = slot.Name,
                Date = slot.Date,
                Time = slot.Time,
                Status = SessionStatus.Completed,
                CompletedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            slot.IsCompleted = true;
            slot.StartLabel = "Completed";
        }

        await _repo.SaveMemberDataAsync(_accountId, _memberId, _memberData);
    }

    [RelayCommand]
    private void SelectDay(DateOnly date)
    {
        SelectedDate = date;
        IsSelectedToday = date == _today;
        SelectedDateLabel = IsSelectedToday ? "Today" : date.ToDateTime(TimeOnly.MinValue).ToString("dddd, MMM d");

        foreach (var cell in WeekStrip) cell.IsSelected = cell.Date == date;

        RoutineDefinition? FindRoutine(Guid id) =>
            _shared.Routines.FirstOrDefault(r => r.Id == id) ?? _manufacturer.Routines.FirstOrDefault(r => r.Id == id);
        Exercise? FindExercise(Guid id) =>
            _shared.Exercises.FirstOrDefault(e => e.Id == id) ?? _manufacturer.Exercises.FirstOrDefault(e => e.Id == id);

        // Incomplete workouts first (so what's left to do is what you see first),
        // earliest time first within that — ScheduleResolver already returns
        // occurrences ordered by time.
        var built = new List<(bool IsCompleted, TodaySlotViewModel ViewModel)>();
        foreach (var (routineId, time) in ScheduleResolver.RoutinesFor(_memberData, date, FindRoutine))
        {
            var routine = FindRoutine(routineId);
            if (routine is null) continue;

            var exerciseNames = routine.Exercises.Select(e => FindExercise(e.ExerciseId)?.Name ?? "Exercise").ToList();
            var existingSession = _memberData.Sessions.FirstOrDefault(s =>
                s.RoutineDefinitionId == routineId && s.Date == date && s.Time == time);
            var isCompleted = existingSession?.Status == SessionStatus.Completed;
            var startLabel = existingSession switch
            {
                null => "Start",
                { Status: SessionStatus.Completed } => "Completed",
                _ => "Resume",
            };

            built.Add((isCompleted, new TodaySlotViewModel(
                routineId,
                date,
                time,
                routine.Type == RoutineType.Hiit,
                time.ToString("h:mm tt"),
                routine.Name,
                $"{exerciseNames.Count} exercises",
                exerciseNames,
                startLabel,
                isCompleted,
                ToggleCompletedCommand)));
        }

        // A workout run ad hoc (e.g. via "Browse Workouts" on an otherwise-unscheduled
        // day) creates a WorkoutSession without ever going through ScheduleResolver —
        // surface those here too, so a workout actually done today doesn't stay hidden
        // behind "Rest day" just because it was never on the schedule.
        var covered = built.Select(t => (t.ViewModel.RoutineId, t.ViewModel.Time)).ToHashSet();
        foreach (var session in _memberData.Sessions.Where(s => s.Date == date))
        {
            if (!covered.Add((session.RoutineDefinitionId, session.Time))) continue;
            var routine = FindRoutine(session.RoutineDefinitionId);
            if (routine is null) continue;

            var exerciseNames = routine.Exercises.Select(e => FindExercise(e.ExerciseId)?.Name ?? "Exercise").ToList();
            var isCompleted = session.Status == SessionStatus.Completed;
            var startLabel = isCompleted ? "Completed" : "Resume";

            built.Add((isCompleted, new TodaySlotViewModel(
                session.RoutineDefinitionId,
                date,
                session.Time,
                routine.Type == RoutineType.Hiit,
                session.Time.ToString("h:mm tt"),
                routine.Name,
                $"{exerciseNames.Count} exercises",
                exerciseNames,
                startLabel,
                isCompleted,
                ToggleCompletedCommand)));
        }

        // Replacing the whole collection (rather than Clear() + Add() in place) avoids
        // BindableLayout briefly seeing an empty source mid-rebuild — that transient
        // empty state crashes natively inside WinUI's own child-collection handling
        // (see the same fix in WorkoutsViewModel.Rebuild()).
        var todaySlots = new ObservableCollection<TodaySlotViewModel>();
        foreach (var (_, slotVm) in built.OrderBy(t => t.IsCompleted).ThenBy(t => t.ViewModel.Time))
        {
            todaySlots.Add(slotVm);
        }
        TodaySlots = todaySlots;
        IsRestDay = TodaySlots.Count == 0;
    }

    /// <summary>
    /// Offers every Standard/HIIT routine the member can see (plus an option to
    /// create a new one on the spot if the one they want isn't listed yet), then
    /// asks whether this is a one-time addition (just SelectedDate, at a time
    /// entered on the spot) or a recurring schedule (handed off to the routine's
    /// own builder page, where the full time+recurrence editor lives).
    /// </summary>
    [RelayCommand]
    private async Task AddWorkout()
    {
        var page = Shell.Current?.CurrentPage;
        if (page is null) return;

        if (PastOrFutureWarning() is string warning)
        {
            var continueAdding = await page.DisplayAlertAsync("Different day", warning, "Continue", "Cancel");
            if (!continueAdding) return;
        }

        var options = _shared.Routines.Concat(_manufacturer.Routines)
            .Where(r => r.Visibility == Visibility.Manufacturer || r.OwnerMemberId == _memberId || r.Visibility == Visibility.Account)
            .OrderBy(r => r.Name)
            .ToList();

        const string createNewOption = "+ Create a new workout";
        var choices = options.Select(r => r.Name).Append(createNewOption).ToArray();
        var routineChoice = await page.DisplayActionSheetAsync(
            options.Count == 0 ? "No workouts yet — create one?" : "Add which workout?", "Cancel", null, choices);
        if (routineChoice is null || routineChoice == "Cancel") return;

        if (routineChoice == createNewOption)
        {
            var typeChoice = await page.DisplayActionSheetAsync("What kind of workout?", "Cancel", null, "Standard", "HIIT");
            if (typeChoice is null || typeChoice == "Cancel") return;
            // HomeWorkoutBridge picks this back up once Save/Cancel pops us back
            // here — see the pending-routine check at the end of LoadAsync.
            var route = typeChoice == "HIIT" ? "hiitBuilder" : "standardBuilder";
            await Shell.Current!.GoToAsync($"{route}?fromHome=true");
            return;
        }

        var routine = options.FirstOrDefault(r => r.Name == routineChoice);
        if (routine is null) return;

        await AssignRoutineToScheduleAsync(page, routine);
    }

    /// <summary>Asks one-time-vs-recurring for a specific routine already chosen. A
    /// recurring schedule is handed off to the routine's own builder page — there's
    /// exactly one place that edits a RoutineSchedule (time + recurrence + snooze),
    /// not a second copy of that editor squeezed into an action sheet. A one-time
    /// add stays a fast inline path: just a time, for just this date. Shared by
    /// AddWorkout's own picker and the "add the workout you just created?" prompt
    /// after returning from a builder.</summary>
    private async Task AssignRoutineToScheduleAsync(Page page, RoutineDefinition routine)
    {
        var dateLabel = IsSelectedToday ? "today" : SelectedDate.ToDateTime(TimeOnly.MinValue).ToString("MMM d");
        var oneTimeLabel = $"Just {dateLabel}";
        var frequencyChoice = await page.DisplayActionSheetAsync(
            "One-time, or does this recur?", "Cancel", null, oneTimeLabel, "Set up a recurring schedule…");
        if (frequencyChoice is null || frequencyChoice == "Cancel") return;

        if (frequencyChoice != oneTimeLabel)
        {
            var route = routine.Type == RoutineType.Hiit ? "hiitBuilder" : "standardBuilder";
            await Shell.Current!.GoToAsync($"{route}?routineId={routine.Id}");
            return;
        }

        TimeOnly time;
        while (true)
        {
            var timeText = await page.DisplayPromptAsync("What time?",
                $"Enter a time for {dateLabel} (e.g. 6:30 AM)", "Add", "Cancel", initialValue: "7:00 AM");
            if (timeText is null) return; // cancelled
            if (TimeOnly.TryParse(timeText, out time)) break;
            await page.DisplayAlertAsync("Couldn't read that time", "Try a format like \"6:30 AM\" or \"18:30\".", "OK");
        }

        _memberData.Schedule.OneTimeOverrides.Add(new OneTimeAssignment { Date = SelectedDate, Time = time, RoutineId = routine.Id });
        _memberData.Schedule.UpdatedAt = DateTimeOffset.UtcNow;
        await _repo.SaveMemberDataAsync(_accountId, _memberId, _memberData);

        var hasWorkout = ScheduleResolver.HasAnyWorkout(_memberData, SelectedDate);
        var cell = WeekStrip.FirstOrDefault(c => c.Date == SelectedDate);
        cell?.SetHasWorkout(hasWorkout);
        SelectDay(SelectedDate);
    }

    /// <summary>Consulted by HomePage before starting/editing a workout on a non-today date.</summary>
    public string? PastOrFutureWarning()
    {
        if (IsSelectedToday) return null;
        return SelectedDate < _today
            ? "This workout occurred in the past. Continue anyway?"
            : "This workout is scheduled for the future. Continue anyway?";
    }

}

public partial class WeekDayCellViewModel : ObservableObject
{
    public DateOnly Date { get; }
    public string Label { get; }
    public Color BackgroundColor { get; private set; } = Colors.Transparent;
    public Color DotColor { get; private set; } = Colors.Transparent;
    public Color BorderColor { get; private set; } = Colors.Transparent;
    public bool IsToday { get; }
    public IRelayCommand<DateOnly> SelectCommand { get; }

    private bool _hasWorkout;
    private bool _logged;

    [ObservableProperty] public partial bool IsSelected { get; set; }

    public WeekDayCellViewModel(DateOnly date, string label, bool isToday, bool hasWorkout, bool logged, IRelayCommand<DateOnly> selectCommand)
    {
        Date = date;
        Label = label;
        IsToday = isToday;
        _hasWorkout = hasWorkout;
        _logged = logged;
        SelectCommand = selectCommand;
        IsSelected = isToday;
        Recompute();
    }

    public void SetHasWorkout(bool hasWorkout)
    {
        _hasWorkout = hasWorkout;
        Recompute();
        OnPropertyChanged(nameof(DotColor));
    }

    partial void OnIsSelectedChanged(bool value)
    {
        Recompute();
        OnPropertyChanged(nameof(BackgroundColor));
        OnPropertyChanged(nameof(BorderColor));
    }

    private void Recompute()
    {
        BackgroundColor = IsSelected ? AppColors.Get("ColorAccent900") : AppColors.Get("ColorSurface");
        BorderColor = IsSelected ? AppColors.Get("ColorAccent") : Colors.Transparent;
        DotColor = _logged ? AppColors.Get("ColorAccent")
            : _hasWorkout ? AppColors.Get("ColorNeutral400")
            : AppColors.Get("ColorNeutral800");
    }
}

public partial class TodaySlotViewModel : ObservableObject
{
    public Guid RoutineId { get; }
    public DateOnly Date { get; }
    public TimeOnly Time { get; }
    public bool IsHiit { get; }
    public string TimeLabel { get; }
    public string Name { get; }
    public string CountLabel { get; }
    public List<string> Exercises { get; }
    public IRelayCommand<TodaySlotViewModel> ToggleCompletedCommand { get; }

    [ObservableProperty] public partial string StartLabel { get; set; }
    [ObservableProperty] public partial bool IsCompleted { get; set; }

    public TodaySlotViewModel(Guid routineId, DateOnly date, TimeOnly time, bool isHiit, string timeLabel, string name, string countLabel,
        List<string> exercises, string startLabel, bool isCompleted, IRelayCommand<TodaySlotViewModel> toggleCompletedCommand)
    {
        RoutineId = routineId;
        Date = date;
        Time = time;
        IsHiit = isHiit;
        TimeLabel = timeLabel;
        Name = name;
        CountLabel = countLabel;
        Exercises = exercises;
        StartLabel = startLabel;
        IsCompleted = isCompleted;
        ToggleCompletedCommand = toggleCompletedCommand;
    }
}
