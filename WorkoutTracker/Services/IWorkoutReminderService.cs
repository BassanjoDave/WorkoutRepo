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
using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Services;

/// <summary>
/// Schedules local, device-side notifications for the routines a member has
/// personally turned a reminder on for (MemberData.RoutineSchedules) — fired
/// at that routine's own scheduled time, on whichever days its recurrence
/// rule produces (see ScheduleResolver.OccursOn), repeating unless it's a
/// one-time schedule. No server or push infrastructure involved: this only
/// works while the app has been opened at least once to register the
/// schedule, same as any local-notification approach.
///
/// Android/iOS use Plugin.LocalNotification, which schedules with the OS —
/// reminders fire even if the app isn't running. A "weekly on specific days"
/// or "every day" schedule maps directly onto its native Weekly/Daily repeat;
/// an "every N weeks"/"every N days" schedule (N > 1) has no native
/// equivalent, so the next several individual occurrences are scheduled as
/// one-shot notifications instead — RescheduleAllAsync already re-runs on
/// every Home load, which keeps that topped up. Windows has no equivalent
/// scheduled-notification API for an unpackaged app (Windows App SDK's
/// AppNotificationManager only shows notifications immediately), so the
/// Windows branch below keeps its own in-process timers, one per routine for
/// its single next occurrence, and only fires while WorkoutTracker is
/// actually running — a deliberate, simpler tradeoff over the alternative
/// (a deprecated third-party package plus AUMID/shortcut registration) to
/// get real scheduled reminders on Windows without either. Mac Catalyst has
/// no implementation at all — IsSupported is false there.
/// </summary>
public interface IWorkoutReminderService
{
    bool IsSupported { get; }
    Task<bool> AreNotificationsEnabledAsync();
    Task<bool> RequestPermissionAsync();

    /// <summary>Cancels every reminder this service could have scheduled, then re-schedules from the current RoutineSchedules/ReminderSettings.</summary>
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
    private const int MaxOneShotOccurrences = 8;

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

        foreach (var (routineId, schedule) in memberData.RoutineSchedules)
        {
            if (!schedule.ReminderEnabled) continue;
            var name = findRoutineName(routineId);
            if (name is null) continue;
            await ScheduleRoutineAsync(routineId, schedule, name);
        }
    }

    private static async Task ScheduleRoutineAsync(Guid routineId, RoutineSchedule schedule, string name)
    {
        var description = ReminderText(name, schedule.Time);
        var payload = JsonSerializer.Serialize(new SnoozePayload(description, schedule.SnoozeMinutes));

        async Task ShowAsync(int notificationId, DateTimeOffset notifyTime, NotificationRepeat repeat)
        {
            var request = new NotificationRequest
            {
                NotificationId = notificationId,
                Title = "Workout reminder",
                Description = description,
                ReturningData = payload,
                Schedule = new NotificationRequestSchedule { NotifyTime = notifyTime, RepeatType = repeat },
            };
            // Only notifications in this category get the Snooze action button — see ReminderCategory below.
            if (schedule.SnoozeEnabled) request.CategoryType = NotificationCategoryType.Reminder;
            await LocalNotificationCenter.Current.Show(request);
        }

        if (schedule.Kind == RecurrenceKind.EveryNDays && schedule.IntervalDays <= 1)
        {
            // Every day — a single native daily repeat.
            await ShowAsync(NotificationId(routineId, "daily"), NextTimeToday(schedule.Time), NotificationRepeat.Daily);
            return;
        }

        if (schedule.Kind == RecurrenceKind.WeeklyOnDays && schedule.IntervalWeeks <= 1)
        {
            // Every week on specific days — one native weekly repeat per weekday.
            foreach (var weekday in schedule.Weekdays)
            {
                await ShowAsync(NotificationId(routineId, weekday), NextOccurrence(weekday, schedule.Time), NotificationRepeat.Weekly);
            }
            return;
        }

        // "Every N weeks"/"every N days" (N > 1) — schedule the next several
        // one-shot occurrences; see the type-level doc comment for why.
        var today = DateOnly.FromDateTime(DateTime.Today);
        var scheduled = 0;
        for (var date = today; scheduled < MaxOneShotOccurrences && date < today.AddYears(1); date = date.AddDays(1))
        {
            if (!ScheduleResolver.OccursOn(schedule, date)) continue;
            var notifyTime = new DateTimeOffset(date.ToDateTime(schedule.Time));
            if (notifyTime <= DateTimeOffset.Now) continue;
            await ShowAsync(NotificationId(routineId, date), notifyTime, NotificationRepeat.No);
            scheduled++;
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

    // One-shot timer per routine's single next occurrence; RescheduleAllAsync
    // disposes and rebuilds all of them, mirroring Android's cancel-then-reschedule.
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

        foreach (var (routineId, schedule) in memberData.RoutineSchedules)
        {
            if (!schedule.ReminderEnabled) continue;
            var name = findRoutineName(routineId);
            if (name is null) continue;
            ScheduleNextOccurrence(routineId, schedule, name);
        }
        return Task.CompletedTask;
    }

    // Recurrence pattern isn't distinguished here beyond "what's the next date this
    // schedule actually occurs on" (ScheduleResolver.OccursOn handles every
    // pattern uniformly): this fallback only ever schedules a single timer for
    // that next upcoming occurrence (see the type-level doc comment — it only
    // fires while the app is actually running, unlike the real OS-level scheduling
    // Android/iOS get), recomputed every time RescheduleAllAsync runs.
    private void ScheduleNextOccurrence(Guid routineId, RoutineSchedule schedule, string name)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        DateOnly? nextDate = null;
        for (var date = today; date < today.AddYears(1); date = date.AddDays(1))
        {
            if (!ScheduleResolver.OccursOn(schedule, date)) continue;
            if (date.ToDateTime(schedule.Time) <= DateTime.Now) continue;
            nextDate = date;
            break;
        }
        if (nextDate is not DateOnly occurrenceDate) return;

        var description = ReminderText(name, schedule.Time);
        var fireAt = new DateTimeOffset(occurrenceDate.ToDateTime(schedule.Time));
        var delay = fireAt - DateTimeOffset.Now;
        if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;

        var snoozeMinutes = schedule.SnoozeEnabled ? schedule.SnoozeMinutes : (int?)null;
        // Timer callbacks run on an arbitrary thread-pool thread. AppNotificationManager
        // is a WinRT API with thread-affinity requirements — calling it off the UI
        // thread risks a native COM crash that bypasses try/catch entirely, not just
        // a catchable managed exception. MainThread.BeginInvokeOnMainThread first,
        // *then* the try/catch is still kept as a second line of defense.
        var timer = new Timer(_ => MainThread.BeginInvokeOnMainThread(() => SafeShow(description, snoozeMinutes)),
            null, delay, Timeout.InfiniteTimeSpan);
        lock (_timersLock) { _timers[NotificationId(routineId, occurrenceDate)] = timer; }
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
    private static string ReminderText(string routineName, TimeOnly time) =>
        $"Time for {routineName} ({time:h:mm tt})";

    private static DateTimeOffset NextOccurrence(Weekday day, TimeOnly reminderTime)
    {
        var today = DateTime.Today;
        var daysUntil = ((int)day - (int)today.DayOfWeek + 7) % 7;
        var anchor = today.AddDays(daysUntil).Add(reminderTime.ToTimeSpan());
        var candidate = new DateTimeOffset(anchor);
        return candidate <= DateTimeOffset.Now ? candidate.AddDays(7) : candidate;
    }

    private static DateTimeOffset NextTimeToday(TimeOnly time)
    {
        var candidate = new DateTimeOffset(DateTime.Today.Add(time.ToTimeSpan()));
        return candidate <= DateTimeOffset.Now ? candidate.AddDays(1) : candidate;
    }

    private static int NotificationId(Guid routineId, object? discriminator) =>
        5000 + (HashCode.Combine(routineId, discriminator) & 0x0FFFFFFF);
#endif
}
