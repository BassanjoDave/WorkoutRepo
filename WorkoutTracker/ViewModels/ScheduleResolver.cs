using WorkoutTracker.Models;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// Resolves which routines apply to a given date/slot. A one-time override
/// only replaces the specific recurring routine it was forked from (via
/// RoutineDefinition.SourceRoutineId) — e.g. a mid-session "just this
/// occurrence" edit swaps in the forked copy for that one slot-mate without
/// hiding whatever else is also scheduled there. An override with no such
/// source (a "just this date" one-off addition) adds alongside the recurring
/// list instead of replacing anything.
/// </summary>
public static class ScheduleResolver
{
    public static List<Guid> RoutinesFor(Schedule schedule, DateOnly date, Slot slot, Func<Guid, RoutineDefinition?> findRoutine)
    {
        var weekday = (Weekday)date.DayOfWeek;
        schedule.Days.TryGetValue(weekday, out var daySlots);
        var recurring = (slot == Slot.Am ? daySlots?.Am : daySlots?.Pm) ?? new List<Guid>();

        var overrides = schedule.OneTimeOverrides.Where(o => o.Date == date && o.Slot == slot).ToList();
        if (overrides.Count == 0) return recurring;

        var result = new List<Guid>(recurring);
        foreach (var o in overrides)
        {
            var sourceId = findRoutine(o.RoutineId)?.SourceRoutineId;
            if (sourceId is Guid sid && result.Remove(sid))
            {
                result.Add(o.RoutineId);
            }
            else if (!result.Contains(o.RoutineId))
            {
                result.Add(o.RoutineId);
            }
        }
        return result;
    }

    public static bool HasAnyWorkout(Schedule schedule, DateOnly date)
    {
        var weekday = (Weekday)date.DayOfWeek;
        var hasRecurring = schedule.Days.TryGetValue(weekday, out var slots) && (slots.Am.Count > 0 || slots.Pm.Count > 0);
        var hasOneTime = schedule.OneTimeOverrides.Any(o => o.Date == date);
        return hasRecurring || hasOneTime;
    }
}
