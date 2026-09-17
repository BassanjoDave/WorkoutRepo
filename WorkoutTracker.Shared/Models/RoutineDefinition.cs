namespace WorkoutTracker.Models;

/// <summary>
/// The template. Finishing a workout never writes here — see WorkoutSession.
/// </summary>
public class RoutineDefinition
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }

    /// <summary>Null for manufacturer-seeded routines.</summary>
    public Guid? OwnerMemberId { get; set; }
    public string? OwnerNameSnapshot { get; set; }

    /// <summary>Set when a copy-on-modify fork created this routine from another member's.</summary>
    public Guid? SourceRoutineId { get; set; }
    public string? SourceNameSnapshot { get; set; }

    /// <summary>The source's exercise "identity" set (exercise ids for Standard,
    /// Exercise-type section titles for HIIT) captured at fork time — see
    /// RoutineNamingHelper. Non-null only while this fork still counts as
    /// "the same workout, just my numbers": present from the moment a copy is
    /// made, cleared the first time an exercise is actually added or removed,
    /// at which point the fork is promoted to a fully independent custom
    /// workout (auto-renamed to "[owner]'s [title]" if the name hasn't
    /// already been changed away from the source's).</summary>
    public List<string>? SourceExerciseKeysSnapshot { get; set; }

    public string Name { get; set; } = "";
    public Visibility Visibility { get; set; } = Visibility.Private;
    public RoutineType Type { get; set; } = RoutineType.Standard;

    public List<RoutineExerciseTarget> Exercises { get; set; } = new();
    public List<HiitSection>? Sections { get; set; }
    public int? CycleRepeats { get; set; }
    public int? RestBetweenCyclesSeconds { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>
/// The author's intended sets/reps for one exercise in a routine — a target,
/// never a log. Actual performed values live on WorkoutSession.Entries.
/// </summary>
public class RoutineExerciseTarget
{
    public Guid ExerciseId { get; set; }
    public List<SetGroupTarget> Groups { get; set; } = new();

    // Cardio/vibration-plate authoring defaults, carried on the target so a
    // fresh session has something to seed its own instance fields from.
    public int? DurationSeconds { get; set; }
    public int? FrequencyHz { get; set; }
    public int? Level { get; set; }
    public string? Stance { get; set; }
    public string? Mode { get; set; }
    public int? TimeMinutes { get; set; }
    public int? Resistance { get; set; }
}

public class SetGroupTarget
{
    /// <summary>Empty string means "not specified" and must round-trip as empty, not zero.</summary>
    public string Sets { get; set; } = "";
    public string Reps { get; set; } = "";
    public string Weight { get; set; } = "";
}

public class HiitSection
{
    public Guid Id { get; set; }
    public string Type { get; set; } = ""; // Warm Up, Exercise, Rest, Cool Down, Custom
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int Seconds { get; set; }
    public bool IsCycleRest { get; set; }

    /// <summary>Hex background color shown behind this section during the workout.
    /// Empty means "use HiitSectionColors.DefaultFor(Type)" — left unset for any
    /// section saved before this feature existed. See HiitSectionColors.Resolve.</summary>
    public string Color { get; set; } = "";
}

/// <summary>
/// The standard warm-up/exercise/rest/cool-down color coding shown behind each HIIT
/// section during a workout — a fixed, non-theme-dependent palette (a "go" green
/// reads as go regardless of light/dark mode) that the builder lets a member override
/// per section (HiitBuilderViewModel.HiitSectionRowViewModel.SelectColorCommand).
/// </summary>
/// <summary>
/// The "copy to mine" naming convention: a straight copy of a manufacturer or
/// another member's routine displays under the source's own name (it's still
/// considered that same workout, just with this member's numbers) until the
/// member actually adds or removes an exercise, at which point it becomes
/// their own custom workout and gets auto-renamed "[member]'s [title]" —
/// unless they'd already renamed it away from the source's name themselves.
/// </summary>
public static class RoutineNamingHelper
{
    /// <summary>The exercise "identity" list to diff against — exercise ids for a
    /// Standard routine, Exercise-type section titles for HIIT (HiitSection has
    /// no exercise id of its own).</summary>
    public static List<string> ExerciseKeysFor(RoutineType type, IEnumerable<Guid> standardExerciseIds, IEnumerable<HiitSection>? hiitSections) =>
        type == RoutineType.Hiit
            ? (hiitSections ?? Enumerable.Empty<HiitSection>()).Where(s => s.Type == "Exercise").Select(s => s.Title).ToList()
            : standardExerciseIds.Select(id => id.ToString()).ToList();

    public static bool HasDivergedFromSource(List<string>? sourceKeysSnapshot, IReadOnlyCollection<string> currentKeys) =>
        sourceKeysSnapshot is not null && !sourceKeysSnapshot.ToHashSet().SetEquals(currentKeys);

    public static string ApplyOwnerPrefix(string ownerName, string title)
    {
        var prefix = $"{ownerName}'s ";
        return title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? title : prefix + title;
    }
}

public static class HiitSectionColors
{
    public const string WarmUp = "#87CEFA";
    public const string Exercise = "#4CAF50";
    public const string Rest = "#F44336";
    public const string CoolDown = WarmUp;
    public const string Custom = "#9E9E9E";

    public static readonly string[] Palette = { WarmUp, Exercise, Rest, Custom, "#FF9800", "#9C27B0" };

    public static string DefaultFor(string type) => type switch
    {
        "Warm Up" => WarmUp,
        "Cool Down" => CoolDown,
        "Exercise" => Exercise,
        "Rest" => Rest,
        _ => Custom,
    };

    public static string Resolve(HiitSection section) =>
        string.IsNullOrEmpty(section.Color) ? DefaultFor(section.Type) : section.Color;
}
