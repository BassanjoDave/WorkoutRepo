using WorkoutTracker.Models;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// Resolves which routines apply to a given date, and at what time, from a
/// member's recurring RoutineSchedules plus any one-time overrides for that
/// exact date. A one-time override only replaces the specific recurring
/// routine it was forked from (via RoutineDefinition.SourceRoutineId) — e.g.
/// a mid-session "just this occurrence" edit swaps in the forked copy for
/// that one occurrence without hiding anything else scheduled that day. An
/// override with no such source (a "just this date" one-off addition) adds
/// alongside the recurring list instead of replacing anything.
/// </summary>
public static class ScheduleResolver
{
    /// <summary>Whether a routine's recurrence rule produces an occurrence on this date.</summary>
    public static bool OccursOn(RoutineSchedule schedule, DateOnly date)
    {
        if (date < schedule.StartDate) return false;
        if (schedule.EndDate is DateOnly end && date > end) return false;

        return schedule.Kind switch
        {
            RecurrenceKind.EveryNDays => (date.DayNumber - schedule.StartDate.DayNumber) % Math.Max(1, schedule.IntervalDays) == 0,
            RecurrenceKind.WeeklyOnDays => schedule.Weekdays.Contains((Weekday)date.DayOfWeek) && IsOnIntervalWeek(schedule.StartDate, date, Math.Max(1, schedule.IntervalWeeks)),
            _ => false,
        };
    }

    /// <summary>"Every N weeks" — aligned to the calendar week (Sunday-start, matching
    /// Weekday.Sunday == 0) containing StartDate, not to StartDate's exact weekday.</summary>
    private static bool IsOnIntervalWeek(DateOnly startDate, DateOnly date, int intervalWeeks)
    {
        var startWeekBegin = startDate.AddDays(-(int)startDate.DayOfWeek);
        var dateWeekBegin = date.AddDays(-(int)date.DayOfWeek);
        var weeksBetween = (dateWeekBegin.DayNumber - startWeekBegin.DayNumber) / 7;
        return weeksBetween % intervalWeeks == 0;
    }

    public static List<(Guid RoutineId, TimeOnly Time)> RoutinesFor(MemberData memberData, DateOnly date, Func<Guid, RoutineDefinition?> findRoutine)
    {
        var overrides = memberData.Schedule.OneTimeOverrides.Where(o => o.Date == date).ToList();

        var suppressed = new HashSet<Guid>();
        foreach (var o in overrides)
        {
            if (findRoutine(o.RoutineId)?.SourceRoutineId is Guid sourceId) suppressed.Add(sourceId);
        }

        var result = memberData.RoutineSchedules
            .Where(kvp => !suppressed.Contains(kvp.Key) && OccursOn(kvp.Value, date))
            .Select(kvp => (RoutineId: kvp.Key, Time: kvp.Value.Time))
            .ToList();

        result.AddRange(overrides.Select(o => (o.RoutineId, o.Time)));

        return result.OrderBy(r => r.Time).ToList();
    }

    public static bool HasAnyWorkout(MemberData memberData, DateOnly date) =>
        memberData.RoutineSchedules.Values.Any(s => OccursOn(s, date)) || memberData.Schedule.OneTimeOverrides.Any(o => o.Date == date);

    /// <summary>Human-readable recurrence summary — "Every Mon, Wed, Fri at 6:30 AM",
    /// "Every day at 7:00 AM", "Every 2 weeks on Tue at 6:00 PM" — shared by Profile's
    /// schedule summary and the Schedule overview page.</summary>
    public static string RecurrenceSummary(RoutineSchedule schedule)
    {
        var timeText = schedule.Time.ToString("h:mm tt");
        var recurrenceText = schedule.Kind switch
        {
            RecurrenceKind.EveryNDays when schedule.IntervalDays <= 1 => "Every day",
            RecurrenceKind.EveryNDays => $"Every {schedule.IntervalDays} days",
            RecurrenceKind.WeeklyOnDays when schedule.Weekdays.Count == 0 => "Weekly",
            RecurrenceKind.WeeklyOnDays =>
                $"Every {(schedule.IntervalWeeks > 1 ? $"{schedule.IntervalWeeks} weeks on " : "")}" +
                string.Join(", ", schedule.Weekdays.OrderBy(d => (int)d).Select(d => d.ToString()[..3])),
            _ => "",
        };
        return $"{recurrenceText} at {timeText}";
    }
}
