namespace WorkoutTracker.Models;

/// <summary>
/// One performed instance of a routine, by one member, on one date/time. This
/// is what Finish Workout writes to — never the RoutineDefinition. Pre-fill
/// for a new session reads the member's own latest WorkoutSession for the
/// same RoutineDefinitionId, so two members sharing a routine never see or
/// overwrite each other's logged values.
/// </summary>
public class WorkoutSession
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid MemberId { get; set; }
    public Guid RoutineDefinitionId { get; set; }
    public string RoutineNameSnapshot { get; set; } = "";
    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.InProgress;
    public DateTimeOffset? CompletedAt { get; set; }
    public List<SessionExerciseEntry> Entries { get; set; } = new();
    public DateTimeOffset UpdatedAt { get; set; }
}

public class SessionExerciseEntry
{
    public Guid ExerciseId { get; set; }
    public bool Done { get; set; }
    public List<SessionSetEntry> Groups { get; set; } = new();

    // Vibration-plate / cardio instance values for this performance.
    public VibrationSettings? Vibration { get; set; }
    public int? CardioTimeMinutes { get; set; }
    public double? CardioDistance { get; set; }
    public int? CardioResistance { get; set; }

    /// <summary>
    /// Set only for entries that aren't backed by a real Exercise — HIIT
    /// sections are timed intervals defined inline on the routine, not
    /// library exercises, so ExerciseId here is a per-section id with
    /// nothing to look up. Display code should prefer this over an
    /// Exercise-library lookup whenever it's present.
    /// </summary>
    public string? Label { get; set; }
}

public class SessionSetEntry
{
    /// <summary>Empty string = not entered. This must stay distinct from "0" through save/reload.</summary>
    public string Sets { get; set; } = "";
    public string Reps { get; set; } = "";
    public string Weight { get; set; } = "";
}

/// <summary>Flat projection of WorkoutSession.Entries, scoped to one member — the History/Log table's data source.</summary>
public class SetLogRow
{
    public Guid MemberId { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; }
    public Guid RoutineDefinitionId { get; set; }
    public string RoutineName { get; set; } = "";
    public Guid ExerciseId { get; set; }
    public string ExerciseName { get; set; } = "";
    public int SetIndex { get; set; }
    public string Sets { get; set; } = "";
    public string Reps { get; set; } = "";
    public string Weight { get; set; } = "";
}
