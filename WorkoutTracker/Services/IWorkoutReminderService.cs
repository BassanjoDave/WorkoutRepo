#if ANDROID || IOS
using System.Text.Json;
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;
using Plugin.LocalNotification.EventArgs;
#elif WINDOWS
using System.Threading;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
#endif
using WorkoutTracker.Models;

namespace WorkoutTracker.Services;

/// <summary>
/// Schedules local, device-side notifications for the routines a member has
/// personally turned a reminder on for (MemberData.RoutineReminders) — one
/// notification per (weekday, slot, routine) that's both scheduled and has an
/// enabled reminder, fired at that routine's own ReminderTime, repeating
/// weekly unless RepeatWeekly is off (fire once, then stop). No server or
/// push infrastructure involved: this only works while the app has been
/// opened at least once to register the schedule, same as any
/// local-notification approach.
///
/// Android/iOS use Plugin.LocalNotification, which schedules with the OS —
/// reminders fire even if the app isn't running. Windows has no equivalent
/// scheduled-notification API for an unpackaged app (Windows App SDK's
/// AppNotificationManager only shows notifications immediately), so the
/// Windows branch below keeps its own in-process timers and only fires while
/// WorkoutTracker is actually running — a deliberate, simpler tradeoff over
/// the alternative (a deprecated third-party package plus AUMID/shortcut
/// registration) to get real scheduled reminders on Windows without either.
/// Mac Catalyst has no implementation at all — IsSupported is false there.
/// </summary>
public interface IWorkoutReminderService
{
    bool IsSupported { get; }
    Task<bool> AreNotificationsEnabledAsync();
    Task<bool> RequestPermissionAsync();

    /// <summary>Cancels every reminder this service could have scheduled, then re-schedules from the current Schedule/ReminderSettings/RoutineReminders.</summary>
    Task RescheduleAllAsync(MemberData memberData, Func<Guid, string?> findRoutineName);
}

public class WorkoutReminderService : IWorkoutReminderService
{
#if ANDROID || IOS
    public bool IsSupported => true;

    // Arbitrary offset so these ids don't collide with any other notification
    // feature this app might add later.
    private const int IdBase = 5000;
    private const int SnoozeActionId = 1;

    public WorkoutReminderService()
    {
        LocalNotificationCenter.Current.NotificationActionTapped += OnNotificationActionTapped;
    }

    public Task<bool> AreNotificationsEnabledAsync() => LocalNotificationCenter.Current.AreNotificationsEnabled();

    public async Task<bool> RequestPermissionAsync()
    {
        if (await AreNotificationsEnabledAsync()) return true;
        return await LocalNotificationCenter.Current.RequestNotificationPermission();
    }

    public async Task RescheduleAllAsync(MemberData memberData, Func<Guid, string?> findRoutineName)
    {
        LocalNotificationCenter.Current.CancelAll();

        if (!memberData.Reminders.Enabled) return;
        if (!await AreNotificationsEnabledAsync()) return;

        foreach (Weekday day in Enum.GetValues<Weekday>())
        {
            memberData.Schedule.Days.TryGetValue(day, out var slots);
            await ScheduleSlotAsync(day, Slot.Am, slots?.Am, memberData.RoutineReminders, findRoutineName);
            await ScheduleSlotAsync(day, Slot.Pm, slots?.Pm, memberData.RoutineReminders, findRoutineName);
        }
    }

    private static async Task ScheduleSlotAsync(Weekday day, Slot slot, List<Guid>? routineIds,
        Dictionary<Guid, RoutineReminderSettings> routineReminders, Func<Guid, string?> findRoutineName)
    {
        if (routineIds is null || routineIds.Count == 0) return;

        foreach (var routineId in routineIds)
        {
            if (!routineReminders.TryGetValue(routineId, out var reminder) || !reminder.Enabled) continue;
            var name = findRoutineName(routineId);
            if (name is null) continue;

            var description = ReminderText(name, slot);
            var payload = JsonSerializer.Serialize(new SnoozePayload(description, reminder.SnoozeMinutes));

            var request = new NotificationRequest
            {
                NotificationId = NotificationId(day, slot, routineId),
                Title = "Workout reminder",
                Description = description,
                ReturningData = payload,
                Schedule = new NotificationRequestSchedule
                {
                    NotifyTime = NextOccurrence(day, reminder.ReminderTime),
                    RepeatType = reminder.RepeatWeekly ? NotificationRepeat.Weekly : NotificationRepeat.No,
                },
            };
            // Only notifications in this category get the Snooze action button — see ReminderCategory below.
            if (reminder.SnoozeEnabled) request.CategoryType = NotificationCategoryType.Reminder;
            await LocalNotificationCenter.Current.Show(request);
        }
    }

    /// <summary>Registered once in MauiProgram via config.AddCategory — any NotificationRequest tagged CategoryType = Reminder gets this Snooze action button.</summary>
    public static readonly NotificationCategory ReminderCategory = new(NotificationCategoryType.Reminder)
    {
        ActionList = new HashSet<NotificationAction>
        {
            new(SnoozeActionId) { Title = "Snooze" },
        },
    };

    private static async void OnNotificationActionTapped(NotificationActionEventArgs e)
    {
        if (e.ActionId != SnoozeActionId) return;
        if (e.Request.ReturningData is not string json) return;

        SnoozePayload payload;
        try { payload = JsonSerializer.Deserialize<SnoozePayload>(json)!; }
        catch { return; }

        var snoozed = new NotificationRequest
        {
            NotificationId = e.Request.NotificationId,
            Title = "Workout reminder",
            Description = payload.Description,
            ReturningData = json,
            CategoryType = e.Request.CategoryType,
            Schedule = new NotificationRequestSchedule
            {
                NotifyTime = DateTime.Now.AddMinutes(payload.SnoozeMinutes),
            },
        };
        await LocalNotificationCenter.Current.Show(snoozed);
    }

    private record SnoozePayload(string Description, int SnoozeMinutes);
#elif WINDOWS
    // Set false if AppNotificationManager registration throws at construction —
    // e.g. the Windows App SDK runtime isn't available on this machine. Every
    // call below becomes a safe no-op rather than crashing the app.
    public bool IsSupported { get; }

    // One-shot timer per (weekday, slot, routine); RescheduleAllAsync disposes
    // and rebuilds all of them, mirroring Android's cancel-then-reschedule.
    // Only fires while this process is alive — see the type-level doc comment.
    private readonly Dictionary<int, Timer> _timers = new();
    private readonly Lock _timersLock = new();

    public WorkoutReminderService()
    {
        try
        {
            AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
            // Registration persists across process restarts (it's a COM/registry
            // entry keyed by this exe), so a second-and-later launch throws here
            // even though everything is already correctly set up. Only actual
            // setup failure (subscribing to the event above) should count as unsupported.
            try { AppNotificationManager.Default.Register(); } catch { }
            IsSupported = true;
        }
        catch
        {
            IsSupported = false;
        }
    }

    public Task<bool> AreNotificationsEnabledAsync()
    {
        if (!IsSupported) return Task.FromResult(false);
        try { return Task.FromResult(AppNotificationManager.Default.Setting == AppNotificationSetting.Enabled); }
        catch { return Task.FromResult(false); }
    }

    public Task<bool> RequestPermissionAsync() => AreNotificationsEnabledAsync();

    public Task RescheduleAllAsync(MemberData memberData, Func<Guid, string?> findRoutineName)
    {
        if (!IsSupported) return Task.CompletedTask;

        lock (_timersLock)
        {
            foreach (var timer in _timers.Values) timer.Dispose();
            _timers.Clear();
        }

        if (!memberData.Reminders.Enabled) return Task.CompletedTask;

        foreach (Weekday day in Enum.GetValues<Weekday>())
        {
            memberData.Schedule.Days.TryGetValue(day, out var slots);
            ScheduleSlot(day, Slot.Am, slots?.Am, memberData.RoutineReminders, findRoutineName);
            ScheduleSlot(day, Slot.Pm, slots?.Pm, memberData.RoutineReminders, findRoutineName);
        }
        return Task.CompletedTask;
    }

    // RepeatWeekly isn't distinguished here: this fallback only ever schedules a single
    // timer for the next upcoming occurrence (see the type-level doc comment — it only
    // fires while the app is actually running, unlike the real OS-level scheduling
    // Android/iOS get), so "weekly" already just means "recomputed next time
    // RescheduleAllAsync runs" rather than a true recurring OS timer either way.
    private void ScheduleSlot(Weekday day, Slot slot, List<Guid>? routineIds,
        Dictionary<Guid, RoutineReminderSettings> routineReminders, Func<Guid, string?> findRoutineName)
    {
        if (routineIds is null || routineIds.Count == 0) return;

        foreach (var routineId in routineIds)
        {
            if (!routineReminders.TryGetValue(routineId, out var reminder) || !reminder.Enabled) continue;
            var name = findRoutineName(routineId);
            if (name is null) continue;

            var description = ReminderText(name, slot);
            var fireAt = NextOccurrence(day, reminder.ReminderTime);
            var delay = fireAt - DateTimeOffset.Now;
            if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;

            var snoozeMinutes = reminder.SnoozeEnabled ? reminder.SnoozeMinutes : (int?)null;
            // Timer callbacks run on an arbitrary thread-pool thread. AppNotificationManager
            // is a WinRT API with thread-affinity requirements — calling it off the UI
            // thread risks a native COM crash that bypasses try/catch entirely, not just
            // a catchable managed exception. MainThread.BeginInvokeOnMainThread first,
            // *then* the try/catch is still kept as a second line of defense.
            var timer = new Timer(_ => MainThread.BeginInvokeOnMainThread(() => SafeShow(description, snoozeMinutes)),
                null, delay, Timeout.InfiniteTimeSpan);
            lock (_timersLock) { _timers[NotificationId(day, slot, routineId)] = timer; }
        }
    }

    private static void SafeShow(string description, int? snoozeMinutes)
    {
        try { Show(description, snoozeMinutes); }
        catch { /* see the comment where this is scheduled — must never throw here */ }
    }

    private static void Show(string description, int? snoozeMinutes)
    {
        var builder = new AppNotificationBuilder()
            .AddText("Workout reminder")
            .AddText(description);
        if (snoozeMinutes is int minutes)
        {
            builder.AddButton(new AppNotificationButton("Snooze")
                .AddArgument("action", "snooze")
                .AddArgument("description", description)
                .AddArgument("minutes", minutes.ToString()));
        }
        AppNotificationManager.Default.Show(builder.BuildNotification());
    }

    private static void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs e)
    {
        try
        {
            if (!e.Arguments.TryGetValue("action", out var action) || action != "snooze") return;
            if (!e.Arguments.TryGetValue("description", out var description)) return;
            if (!e.Arguments.TryGetValue("minutes", out var minutesText) || !int.TryParse(minutesText, out var minutes)) return;

            // Untracked one-off follow-up — RescheduleAllAsync's timer dictionary only
            // needs to cancel the *originals*; letting this one run to completion (or
            // leak until process exit if the app closes first) is an acceptable trade
            // for how rarely someone snoozes and then also edits their schedule
            // within the snooze window.
            _ = new Timer(_ => MainThread.BeginInvokeOnMainThread(() => SafeShow(description, null)),
                null, TimeSpan.FromMinutes(minutes), Timeout.InfiniteTimeSpan);
        }
        catch { /* a WinRT event callback throwing is just as fatal as a Timer callback throwing — never let it */ }
    }
#else
    public bool IsSupported => false;
    public Task<bool> AreNotificationsEnabledAsync() => Task.FromResult(false);
    public Task<bool> RequestPermissionAsync() => Task.FromResult(false);
    public Task RescheduleAllAsync(MemberData memberData, Func<Guid, string?> findRoutineName) => Task.CompletedTask;
#endif

#if ANDROID || IOS || WINDOWS
    private static string ReminderText(string routineName, Slot slot) =>
        $"Time for {routineName} ({(slot == Slot.Am ? "AM" : "PM")})";

    private static DateTimeOffset NextOccurrence(Weekday day, TimeOnly reminderTime)
    {
        var today = DateTime.Today;
        var daysUntil = ((int)day - (int)today.DayOfWeek + 7) % 7;
        var anchor = today.AddDays(daysUntil).Add(reminderTime.ToTimeSpan());
        var candidate = new DateTimeOffset(anchor);
        return candidate <= DateTimeOffset.Now ? candidate.AddDays(7) : candidate;
    }

    private static int NotificationId(Weekday day, Slot slot, Guid routineId) =>
        5000 + (HashCode.Combine(day, slot, routineId) & 0x0FFFFFFF);
#endif
}
