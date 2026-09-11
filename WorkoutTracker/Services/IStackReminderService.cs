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
/// Schedules local, device-side notifications for supplement/medication stacks
/// that have their own reminder turned on — one per (weekday, stack), fired at
/// that stack's own absolute Time (no AM/PM anchor or offset the way workout
/// reminders have, since a stack already carries its own time-of-day). Mirrors
/// IWorkoutReminderService's structure and platform split exactly — see that
/// type's doc comment for why Android/iOS use real OS-scheduled notifications
/// while Windows falls back to in-process timers, and why Mac Catalyst has none.
/// Reuses WorkoutReminderService's already-registered NotificationCategoryType.Reminder
/// category (same Snooze action button), so no extra MauiProgram registration is needed.
/// </summary>
public interface IStackReminderService
{
    bool IsSupported { get; }
    Task<bool> AreNotificationsEnabledAsync();
    Task<bool> RequestPermissionAsync();

    /// <summary>Cancels every reminder this service could have scheduled, then re-schedules from the given stacks.</summary>
    Task RescheduleAllAsync(List<SupplementStack> stacks);

    /// <summary>Cancels one specific stack's own scheduled reminders — call this at the
    /// deletion call site, right before removing the stack from MemberData.Stacks and
    /// saving. RescheduleAllAsync alone can't reach a deleted stack's old notifications
    /// (a stack no longer in the list it's given leaves no trace to cancel by), so a
    /// deleted stack's weekly-repeating reminder would otherwise keep firing forever.</summary>
    Task CancelForStackAsync(SupplementStack stack);
}

public class StackReminderService : IStackReminderService
{
#if ANDROID || IOS
    public bool IsSupported => true;

    public Task<bool> AreNotificationsEnabledAsync() => LocalNotificationCenter.Current.AreNotificationsEnabled();

    public async Task<bool> RequestPermissionAsync()
    {
        if (await AreNotificationsEnabledAsync()) return true;
        return await LocalNotificationCenter.Current.RequestNotificationPermission();
    }

    public async Task RescheduleAllAsync(List<SupplementStack> stacks)
    {
        // Only this service's own id range gets cancelled-and-rebuilt — a shared
        // LocalNotificationCenter.Current.CancelAll() would also wipe workout
        // reminders, which is why each pending id here is cancelled individually.
        foreach (var stack in stacks)
        {
            foreach (var day in DaysFor(stack))
            {
                LocalNotificationCenter.Current.Cancel(NotificationId(day, stack.Id));
            }
        }

        if (!await AreNotificationsEnabledAsync()) return;

        foreach (var stack in stacks)
        {
            if (!stack.ReminderEnabled || stack.Items.Count == 0) continue;

            var description = ReminderText(stack);
            var payload = JsonSerializer.Serialize(new SnoozePayload(description, stack.SnoozeMinutes));

            foreach (var day in DaysFor(stack))
            {
                var request = new NotificationRequest
                {
                    NotificationId = NotificationId(day, stack.Id),
                    Title = "Supplement reminder",
                    Description = description,
                    ReturningData = payload,
                    Schedule = new NotificationRequestSchedule
                    {
                        NotifyTime = NextOccurrence(day, stack.Time),
                        RepeatType = NotificationRepeat.Weekly,
                    },
                };
                if (stack.SnoozeEnabled) request.CategoryType = NotificationCategoryType.Reminder;
                await LocalNotificationCenter.Current.Show(request);
            }
        }
    }

    public Task CancelForStackAsync(SupplementStack stack)
    {
        foreach (var day in DaysFor(stack))
        {
            LocalNotificationCenter.Current.Cancel(NotificationId(day, stack.Id));
        }
        return Task.CompletedTask;
    }

    private record SnoozePayload(string Description, int SnoozeMinutes);
#elif WINDOWS
    public bool IsSupported { get; }

    private readonly Dictionary<int, Timer> _timers = new();
    private readonly Lock _timersLock = new();

    public StackReminderService()
    {
        // AppNotificationManager is already registered by WorkoutReminderService's
        // own constructor (a process-wide COM registration) — this just probes
        // whether that succeeded, without registering a second time.
        try
        {
            _ = AppNotificationManager.Default.Setting;
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

    public Task RescheduleAllAsync(List<SupplementStack> stacks)
    {
        if (!IsSupported) return Task.CompletedTask;

        lock (_timersLock)
        {
            foreach (var timer in _timers.Values) timer.Dispose();
            _timers.Clear();
        }

        foreach (var stack in stacks)
        {
            if (!stack.ReminderEnabled || stack.Items.Count == 0) continue;

            var description = ReminderText(stack);
            var snoozeMinutes = stack.SnoozeEnabled ? stack.SnoozeMinutes : (int?)null;

            foreach (var day in DaysFor(stack))
            {
                var fireAt = NextOccurrence(day, stack.Time);
                var delay = fireAt - DateTimeOffset.Now;
                if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;

                // See WorkoutReminderService's identical comment: timer callbacks run
                // off the UI thread, and AppNotificationManager has thread affinity —
                // MainThread.BeginInvokeOnMainThread first, try/catch as a second line
                // of defense against a native COM crash bypassing it entirely.
                var timer = new Timer(_ => MainThread.BeginInvokeOnMainThread(() => SafeShow(description, snoozeMinutes)),
                    null, delay, Timeout.InfiniteTimeSpan);
                lock (_timersLock) { _timers[NotificationId(day, stack.Id)] = timer; }
            }
        }
        return Task.CompletedTask;
    }

    public Task CancelForStackAsync(SupplementStack stack)
    {
        lock (_timersLock)
        {
            foreach (var day in DaysFor(stack))
            {
                if (_timers.Remove(NotificationId(day, stack.Id), out var timer)) timer.Dispose();
            }
        }
        return Task.CompletedTask;
    }

    private static void SafeShow(string description, int? snoozeMinutes)
    {
        try { Show(description, snoozeMinutes); }
        catch { /* must never throw here — see the comment where this is scheduled */ }
    }

    private static void Show(string description, int? snoozeMinutes)
    {
        var builder = new AppNotificationBuilder()
            .AddText("Supplement reminder")
            .AddText(description);
        if (snoozeMinutes is int minutes)
        {
            builder.AddButton(new AppNotificationButton("Snooze")
                .AddArgument("action", "stack-snooze")
                .AddArgument("description", description)
                .AddArgument("minutes", minutes.ToString()));
        }
        AppNotificationManager.Default.Show(builder.BuildNotification());
    }
#else
    public bool IsSupported => false;
    public Task<bool> AreNotificationsEnabledAsync() => Task.FromResult(false);
    public Task<bool> RequestPermissionAsync() => Task.FromResult(false);
    public Task RescheduleAllAsync(List<SupplementStack> stacks) => Task.CompletedTask;
    public Task CancelForStackAsync(SupplementStack stack) => Task.CompletedTask;
#endif

#if ANDROID || IOS || WINDOWS
    private static IEnumerable<Weekday> DaysFor(SupplementStack stack) =>
        stack.Days.Count == 0 ? Enum.GetValues<Weekday>() : stack.Days;

    private static string ReminderText(SupplementStack stack)
    {
        var itemNames = string.Join(", ", stack.Items.Select(i => i.Name));
        return $"Time for {stack.Name}: {itemNames}";
    }

    private static DateTimeOffset NextOccurrence(Weekday day, TimeOnly time)
    {
        var today = DateTime.Today;
        var daysUntil = ((int)day - (int)today.DayOfWeek + 7) % 7;
        var candidate = new DateTimeOffset(today.AddDays(daysUntil).Add(time.ToTimeSpan()));
        return candidate <= DateTimeOffset.Now ? candidate.AddDays(7) : candidate;
    }

    private static int NotificationId(Weekday day, Guid stackId) =>
        IdBase + (HashCode.Combine(day, stackId) & 0x0FFFFFFF);

    private const int IdBase = 6000;
#endif
}
