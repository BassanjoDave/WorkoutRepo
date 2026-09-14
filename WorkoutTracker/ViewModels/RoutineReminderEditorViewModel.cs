using CommunityToolkit.Mvvm.ComponentModel;
using WorkoutTracker.Models;
using WorkoutTracker.Services;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// A member's personal reminder for one routine — shared by StandardBuilder and
/// HiitBuilder since both let you edit a routine reached from Home, Library, or
/// the Schedule editor, and per Profile > Workout Reminders the reminder itself
/// belongs to "my use of this routine," not to the routine definition or any one
/// scheduled slot. See MemberData.RoutineReminders.
/// </summary>
public partial class RoutineReminderEditorViewModel : ObservableObject
{
    private readonly IWorkoutReminderService _reminders;
    private bool _suppressPermissionCheck;

    [ObservableProperty] public partial bool Enabled { get; set; }
    [ObservableProperty] public partial TimeSpan ReminderTime { get; set; } = new(7, 0, 0);
    [ObservableProperty] public partial bool RepeatWeekly { get; set; } = true;
    [ObservableProperty] public partial bool SnoozeEnabled { get; set; } = true;
    [ObservableProperty] public partial int SnoozeMinutes { get; set; } = 10;

    /// <summary>The shared Profile > Workout Reminders switch. When false, the page hosting this editor should disable (grey out) its reminder controls — the values above stay exactly as set.</summary>
    [ObservableProperty] public partial bool MasterEnabled { get; set; }

    public RoutineReminderEditorViewModel(IWorkoutReminderService reminders)
    {
        _reminders = reminders;
    }

    public void Load(MemberData memberData, Guid routineId)
    {
        _suppressPermissionCheck = true;
        MasterEnabled = memberData.Reminders.Enabled;
        if (memberData.RoutineReminders.TryGetValue(routineId, out var settings))
        {
            Enabled = settings.Enabled;
            ReminderTime = settings.ReminderTime.ToTimeSpan();
            RepeatWeekly = settings.RepeatWeekly;
            SnoozeEnabled = settings.SnoozeEnabled;
            SnoozeMinutes = settings.SnoozeMinutes;
        }
        else
        {
            Enabled = false;
            ReminderTime = new TimeSpan(7, 0, 0);
            RepeatWeekly = true;
            SnoozeEnabled = true;
            SnoozeMinutes = 10;
        }
        _suppressPermissionCheck = false;
    }

    public void ApplyTo(MemberData memberData, Guid routineId)
    {
        memberData.RoutineReminders[routineId] = new RoutineReminderSettings
        {
            Enabled = Enabled,
            ReminderTime = TimeOnly.FromTimeSpan(ReminderTime),
            RepeatWeekly = RepeatWeekly,
            SnoozeEnabled = SnoozeEnabled,
            SnoozeMinutes = SnoozeMinutes,
        };
    }

    partial void OnEnabledChanged(bool value)
    {
        if (_suppressPermissionCheck || !value || _reminders.IsSupported) return;

        _suppressPermissionCheck = true;
        Enabled = false;
        _suppressPermissionCheck = false;

        var page = Shell.Current?.CurrentPage;
        if (page is not null)
        {
            _ = page.DisplayAlertAsync("Not available here",
                "Workout reminders need Android or iOS — this device/platform doesn't support local notifications through this app yet.", "OK");
        }
    }
}
