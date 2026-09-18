using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;

namespace WorkoutTracker.ViewModels;

/// <summary>Which of the three recurrence shapes the UI offers — "Every day" is just
/// EveryNDays with IntervalDays=1 under the hood, split out here purely so it's a
/// one-tap chip instead of "every N days" with N typed in as 1.</summary>
public enum RepeatOption { EveryDay, EveryNDays, WeeklyOnDays }

/// <summary>
/// A member's personal schedule + reminder for one routine — shared by
/// StandardBuilder and HiitBuilder since both let you edit a routine reached
/// from Home, Library, or the Schedule overview. Merges what used to be two
/// separate things (a day/AM-PM grid entry, and a loosely-linked reminder
/// time) into one Outlook-style time+recurrence+snooze editor: per Profile >
/// Workout Reminders, this belongs to "my use of this routine," not to the
/// routine definition. See MemberData.RoutineSchedules.
/// </summary>
public partial class RoutineReminderEditorViewModel : ObservableObject
{
    private readonly IWorkoutReminderService _reminders;
    private bool _suppressPermissionCheck;
    private DateOnly _startDate = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Whether this routine recurs at all — off means unscheduled (still
    /// runnable ad hoc from the Rituals list), and nothing below matters.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    public partial bool IsScheduled { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    public partial TimeSpan Time { get; set; } = new(7, 0, 0);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEveryNDays))]
    [NotifyPropertyChangedFor(nameof(IsWeeklyOnDays))]
    [NotifyPropertyChangedFor(nameof(Summary))]
    public partial RepeatOption Repeat { get; set; } = RepeatOption.WeeklyOnDays;
    public bool IsEveryNDays => Repeat == RepeatOption.EveryNDays;
    public bool IsWeeklyOnDays => Repeat == RepeatOption.WeeklyOnDays;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    public partial int IntervalDays { get; set; } = 2;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    public partial int IntervalWeeks { get; set; } = 1;
    public ObservableCollection<WeekdayChipViewModel> WeekdayChips { get; }

    /// <summary>Live "Every Mon, Wed, Fri at 6:30 AM"-style summary of the current,
    /// not-yet-saved editor state — shown on the main builder page's "Schedule &amp;
    /// Reminder" nav row so the collapsed page still shows what's set at a glance.
    /// Reuses ScheduleResolver.RecurrenceSummary against a throwaway RoutineSchedule
    /// built from these fields, the same shape ApplyTo saves.</summary>
    public string Summary => IsScheduled
        ? ScheduleResolver.RecurrenceSummary(new RoutineSchedule
        {
            Time = TimeOnly.FromTimeSpan(Time),
            Kind = Repeat == RepeatOption.WeeklyOnDays ? RecurrenceKind.WeeklyOnDays : RecurrenceKind.EveryNDays,
            Weekdays = WeekdayChips.Where(c => c.IsSelected).Select(c => c.Weekday).ToList(),
            IntervalDays = Repeat == RepeatOption.EveryDay ? 1 : Math.Max(2, IntervalDays),
            IntervalWeeks = Math.Max(1, IntervalWeeks),
        })
        : "Not scheduled";

    [ObservableProperty] public partial bool HasEndDate { get; set; }
    [ObservableProperty] public partial DateTime EndDate { get; set; } = DateTime.Today.AddMonths(1);

    [ObservableProperty] public partial bool ReminderEnabled { get; set; } = true;
    [ObservableProperty] public partial bool SnoozeEnabled { get; set; } = true;
    [ObservableProperty] public partial int SnoozeMinutes { get; set; } = 10;

    /// <summary>The shared Profile > Workout Reminders switch. When false, the page hosting this editor should disable (grey out) its reminder controls — the values above stay exactly as set.</summary>
    [ObservableProperty] public partial bool MasterEnabled { get; set; }

    public RoutineReminderEditorViewModel(IWorkoutReminderService reminders)
    {
        _reminders = reminders;
        WeekdayChips = new ObservableCollection<WeekdayChipViewModel>(
            Enum.GetValues<Weekday>().Select(d => new WeekdayChipViewModel(d, ToggleWeekdayCommand)));
    }

    [RelayCommand]
    private void ToggleWeekday(WeekdayChipViewModel chip)
    {
        chip.IsSelected = !chip.IsSelected;
        OnPropertyChanged(nameof(Summary));
    }

    [RelayCommand]
    private void SetRepeat(string mode) => Repeat = Enum.Parse<RepeatOption>(mode);

    public void Load(MemberData memberData, Guid routineId)
    {
        _suppressPermissionCheck = true;
        MasterEnabled = memberData.Reminders.Enabled;

        if (memberData.RoutineSchedules.TryGetValue(routineId, out var schedule))
        {
            IsScheduled = true;
            Time = schedule.Time.ToTimeSpan();
            _startDate = schedule.StartDate;
            HasEndDate = schedule.EndDate is not null;
            EndDate = (schedule.EndDate ?? _startDate.AddMonths(1)).ToDateTime(TimeOnly.MinValue);
            ReminderEnabled = schedule.ReminderEnabled;
            SnoozeEnabled = schedule.SnoozeEnabled;
            SnoozeMinutes = schedule.SnoozeMinutes;

            if (schedule.Kind == RecurrenceKind.EveryNDays)
            {
                Repeat = schedule.IntervalDays <= 1 ? RepeatOption.EveryDay : RepeatOption.EveryNDays;
                IntervalDays = Math.Max(2, schedule.IntervalDays);
            }
            else
            {
                Repeat = RepeatOption.WeeklyOnDays;
                IntervalWeeks = Math.Max(1, schedule.IntervalWeeks);
            }
            foreach (var chip in WeekdayChips) chip.IsSelected = schedule.Weekdays.Contains(chip.Weekday);
        }
        else
        {
            IsScheduled = false;
            Time = new TimeSpan(7, 0, 0);
            _startDate = DateOnly.FromDateTime(DateTime.Today);
            HasEndDate = false;
            EndDate = DateTime.Today.AddMonths(1);
            Repeat = RepeatOption.WeeklyOnDays;
            IntervalDays = 2;
            IntervalWeeks = 1;
            ReminderEnabled = true;
            SnoozeEnabled = true;
            SnoozeMinutes = 10;
            foreach (var chip in WeekdayChips) chip.IsSelected = false;
        }
        _suppressPermissionCheck = false;
    }

    public void ApplyTo(MemberData memberData, Guid routineId)
    {
        if (!IsScheduled)
        {
            memberData.RoutineSchedules.Remove(routineId);
            return;
        }

        memberData.RoutineSchedules[routineId] = new RoutineSchedule
        {
            Time = TimeOnly.FromTimeSpan(Time),
            Kind = Repeat == RepeatOption.WeeklyOnDays ? RecurrenceKind.WeeklyOnDays : RecurrenceKind.EveryNDays,
            Weekdays = WeekdayChips.Where(c => c.IsSelected).Select(c => c.Weekday).ToList(),
            IntervalDays = Repeat == RepeatOption.EveryDay ? 1 : Math.Max(2, IntervalDays),
            IntervalWeeks = Math.Max(1, IntervalWeeks),
            StartDate = _startDate,
            EndDate = HasEndDate ? DateOnly.FromDateTime(EndDate) : null,
            ReminderEnabled = ReminderEnabled,
            SnoozeEnabled = SnoozeEnabled,
            SnoozeMinutes = SnoozeMinutes,
        };
    }

    partial void OnReminderEnabledChanged(bool value)
    {
        if (_suppressPermissionCheck || !value || _reminders.IsSupported) return;

        _suppressPermissionCheck = true;
        ReminderEnabled = false;
        _suppressPermissionCheck = false;

        var page = Shell.Current?.CurrentPage;
        if (page is not null)
        {
            _ = page.DisplayAlertAsync("Not available here",
                "Workout reminders need Android or iOS — this device/platform doesn't support local notifications through this app yet.", "OK");
        }
    }
}

public partial class WeekdayChipViewModel : ObservableObject
{
    public Weekday Weekday { get; }
    public string Label { get; }
    public IRelayCommand<WeekdayChipViewModel> ToggleCommand { get; }

    [ObservableProperty] public partial bool IsSelected { get; set; }

    public WeekdayChipViewModel(Weekday weekday, IRelayCommand<WeekdayChipViewModel> toggleCommand)
    {
        Weekday = weekday;
        Label = weekday.ToString()[..3];
        ToggleCommand = toggleCommand;
    }
}
