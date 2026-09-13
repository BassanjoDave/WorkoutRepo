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

    [ObservableProperty] public partial string ActiveMemberInitial { get; set; } = "";
    [ObservableProperty] public partial Color ActiveMemberColor { get; set; } = Colors.Gray;
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
    [ObservableProperty] public partial int UnreadNotificationCount { get; set; }
    public bool HasUnreadNotifications => UnreadNotificationCount > 0;
    partial void OnUnreadNotificationCountChanged(int value) => OnPropertyChanged(nameof(HasUnreadNotifications));

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

        ActiveMemberInitial = member.Initial;
        ActiveMemberColor = Color.FromArgb(member.AvatarColor);

        _memberData = await _repo.GetMemberDataAsync(account.Id, member.Id);
        _shared = await _repo.GetSharedLibraryAsync(account.Id);
        _manufacturer = await _repo.GetManufacturerLibraryAsync();

        UnreadNotificationCount = _memberData.Notifications.Count(n => !n.IsRead);

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
            var hasWorkout = ScheduleResolver.HasAnyWorkout(_memberData.Schedule, dateOnly);
            var logged = completedDates.Contains(dateOnly);
            var isToday = dateOnly == _today;
            weekStrip.Add(new WeekDayCellViewModel(dateOnly, day.ToString("ddd")[..1].ToUpperInvariant(), isToday, hasWorkout, logged, SelectDayCommand));
        }
        WeekStrip = weekStrip;

        SelectDay(_today);

        // Fire-and-forget: keeps device-side reminders in sync with whatever
        // the schedule looks like right now, without threading the reminder
        // service through every place that can change the schedule.
        _ = _reminders.RescheduleAllAsync(_memberData, id =>
            _shared.Routines.FirstOrDefault(r => r.Id == id)?.Name ?? _manufacturer.Routines.FirstOrDefault(r => r.Id == id)?.Name);

        // Reappearing after "+ Add Workout" → "Create a new workout" sent the member
        // to build one — see IHomeWorkoutBridge. Offer to put it on today's schedule
        // right away instead of making them repeat the same "+ Add Workout" trip.
        if (_homeWorkoutBridge.ConsumePendingRoutineId() is Guid newRoutineId)
        {
            var newRoutine = _shared.Routines.FirstOrDefault(r => r.Id == newRoutineId)
                ?? _manufacturer.Routines.FirstOrDefault(r => r.Id == newRoutineId);
            if (newRoutine is not null && Shell.Current?.CurrentPage is Page page)
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
    private async Task OpenNotifications() => await Shell.Current.GoToAsync("notifications");

    [RelayCommand]
    private void GoToToday() => SelectDay(_today);

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
        // earliest slot first within each group — AM naturally sorts before PM.
        var built = new List<(bool IsCompleted, TodaySlotViewModel ViewModel)>();
        foreach (var (slot, routineIds) in new[]
                 {
                     (Slot.Am, ScheduleResolver.RoutinesFor(_memberData.Schedule, date, Slot.Am, FindRoutine)),
                     (Slot.Pm, ScheduleResolver.RoutinesFor(_memberData.Schedule, date, Slot.Pm, FindRoutine)),
                 })
        {
            foreach (var routineId in routineIds)
            {
                var routine = FindRoutine(routineId);
                if (routine is null) continue;

                var exerciseNames = routine.Exercises.Select(e => FindExercise(e.ExerciseId)?.Name ?? "Exercise").ToList();
                var existingSession = _memberData.Sessions.FirstOrDefault(s =>
                    s.RoutineDefinitionId == routineId && s.Date == date && s.Slot == slot);
                var isCompleted = existingSession?.Status == SessionStatus.Completed;
                var startLabel = existingSession switch
                {
                    null => "Start",
                    { Status: SessionStatus.Completed } => "Completed",
                    _ => "Resume",
                };

                built.Add((isCompleted, new TodaySlotViewModel(
                    routineId,
                    slot,
                    routine.Type == RoutineType.Hiit,
                    slot == Slot.Am ? "AM" : "PM",
                    routine.Name,
                    $"{exerciseNames.Count} exercises",
                    exerciseNames,
                    startLabel)));
            }
        }

        // Replacing the whole collection (rather than Clear() + Add() in place) avoids
        // BindableLayout briefly seeing an empty source mid-rebuild — that transient
        // empty state crashes natively inside WinUI's own child-collection handling
        // (see the same fix in WorkoutsViewModel.Rebuild()).
        var todaySlots = new ObservableCollection<TodaySlotViewModel>();
        foreach (var (_, slotVm) in built.OrderBy(t => t.IsCompleted).ThenBy(t => t.ViewModel.Slot))
        {
            todaySlots.Add(slotVm);
        }
        TodaySlots = todaySlots;
        IsRestDay = TodaySlots.Count == 0;
    }

    /// <summary>
    /// Offers every Standard/HIIT routine the member can see (plus an option to
    /// create a new one on the spot if the one they want isn't listed yet), asks
    /// AM or PM, then asks whether this is a one-time addition (just SelectedDate)
    /// or a standing weekly assignment (every weekday-of-SelectedDate, that slot).
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

    /// <summary>Asks AM/PM and one-time-vs-recurring for a specific routine already
    /// chosen, then applies it to the schedule. Shared by AddWorkout's own picker and
    /// the "add the workout you just created?" prompt after returning from a builder.</summary>
    private async Task AssignRoutineToScheduleAsync(Page page, RoutineDefinition routine)
    {
        var slotChoice = await page.DisplayActionSheetAsync("Add to which slot?", "Cancel", null, "AM", "PM");
        if (slotChoice is null || slotChoice == "Cancel") return;
        var slot = slotChoice == "AM" ? Slot.Am : Slot.Pm;

        var weekday = (Weekday)SelectedDate.DayOfWeek;
        var dateLabel = IsSelectedToday ? "today" : SelectedDate.ToDateTime(TimeOnly.MinValue).ToString("MMM d");
        var recurringLabel = $"Every {weekday} {(slot == Slot.Am ? "AM" : "PM")}";
        var frequencyChoice = await page.DisplayActionSheetAsync(
            "One-time, or every week?", "Cancel", null, $"Just {dateLabel}", recurringLabel);
        if (frequencyChoice is null || frequencyChoice == "Cancel") return;

        if (frequencyChoice == recurringLabel)
        {
            if (!_memberData.Schedule.Days.TryGetValue(weekday, out var daySlots))
            {
                daySlots = new DaySlots();
                _memberData.Schedule.Days[weekday] = daySlots;
            }
            var list = slot == Slot.Am ? daySlots.Am : daySlots.Pm;
            if (!list.Contains(routine.Id)) list.Add(routine.Id);
        }
        else
        {
            _memberData.Schedule.OneTimeOverrides.Add(new OneTimeAssignment { Date = SelectedDate, Slot = slot, RoutineId = routine.Id });
        }
        _memberData.Schedule.UpdatedAt = DateTimeOffset.UtcNow;
        await _repo.SaveMemberDataAsync(_accountId, _memberId, _memberData);

        var hasWorkout = ScheduleResolver.HasAnyWorkout(_memberData.Schedule, SelectedDate);
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

public class TodaySlotViewModel
{
    public Guid RoutineId { get; }
    public Slot Slot { get; }
    public bool IsHiit { get; }
    public string SlotLabel { get; }
    public string Name { get; }
    public string CountLabel { get; }
    public List<string> Exercises { get; }
    public string StartLabel { get; }

    public TodaySlotViewModel(Guid routineId, Slot slot, bool isHiit, string slotLabel, string name, string countLabel, List<string> exercises, string startLabel)
    {
        RoutineId = routineId;
        Slot = slot;
        IsHiit = isHiit;
        SlotLabel = slotLabel;
        Name = name;
        CountLabel = countLabel;
        Exercises = exercises;
        StartLabel = startLabel;
    }
}
