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
    [ObservableProperty] public partial ReminderOffset Offset { get; set; } = ReminderOffset.AtTime;
    [ObservableProperty] public partial bool SnoozeEnabled { get; set; } = true;
    [ObservableProperty] public partial int SnoozeMinutes { get; set; } = 10;

    /// <summary>The shared Profile > Workout Reminders switch. When false, the page hosting this editor should disable (grey out) its reminder controls — the values above stay exactly as set.</summary>
    [ObservableProperty] public partial bool MasterEnabled { get; set; }

    // Display strings, in ReminderOffset declaration order, so OffsetIndex below
    // can convert straight to/from the enum with a cast instead of a converter.
    public string[] OffsetLabels { get; } = { "At time of workout", "5 minutes before", "15 minutes before", "30 minutes before", "1 hour before" };

    public int OffsetIndex
    {
        get => (int)Offset;
        set => Offset = (ReminderOffset)value;
    }

    partial void OnOffsetChanged(ReminderOffset value) => OnPropertyChanged(nameof(OffsetIndex));

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
            Offset = settings.Offset;
            SnoozeEnabled = settings.SnoozeEnabled;
            SnoozeMinutes = settings.SnoozeMinutes;
        }
        else
        {
            Enabled = false;
            Offset = ReminderOffset.AtTime;
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
            Offset = Offset,
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
