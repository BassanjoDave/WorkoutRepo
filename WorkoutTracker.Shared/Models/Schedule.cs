namespace WorkoutTracker.Models;

public enum Weekday { Sunday, Monday, Tuesday, Wednesday, Thursday, Friday, Saturday }

/// <summary>One member's weekly AM/PM routine assignments.</summary>
public class Schedule
{
    public Guid AccountId { get; set; }
    public Guid MemberId { get; set; }
    public Dictionary<Weekday, DaySlots> Days { get; set; } = new();

    /// <summary>
    /// Assignments that apply to one specific calendar date/slot only, taking
    /// priority over the recurring Days entry for that weekday/slot when
    /// present. Used for "just this one day" workouts added from Home, and
    /// for "just this occurrence" edits made mid-session (a forked routine
    /// assigned here instead of mutating the recurring routine everyone/every
    /// week sees).
    /// </summary>
    public List<OneTimeAssignment> OneTimeOverrides { get; set; } = new();

    public DateTimeOffset UpdatedAt { get; set; }
}

public class DaySlots
{
    public List<Guid> Am { get; set; } = new();
    public List<Guid> Pm { get; set; } = new();
}

public class OneTimeAssignment
{
    public DateOnly Date { get; set; }
    public Slot Slot { get; set; }
    public Guid RoutineId { get; set; }
}

/// <summary>Per-member data bundle — the shape of one members/{memberId}.json document.</summary>
public class MemberData
{
    public Schedule Schedule { get; set; } = new();
    public List<WorkoutSession> Sessions { get; set; } = new();
    public HiitSettings HiitSettings { get; set; } = new();
    public string WeightUnit { get; set; } = "lbs";

    /// <summary>
    /// Which equipment/manufacturer keys (enum names, or "custom:{guid}" for
    /// CustomEquipmentType entries) the Library should show. Null means "not
    /// customized yet" — show everything, so existing members see no change
    /// until they visit the equipment preferences screen.
    /// </summary>
    public List<string>? LibraryEquipmentFilter { get; set; }

    /// <summary>Rig ids this member has added to their own collection — see Rig/RigCatalog.
    /// Adding one unlocks its EquipmentKey in LibraryEquipmentFilter if that filter has been
    /// customized (see MyRigsViewModel); removing a Rig does NOT retroactively re-lock it,
    /// consistent with this app's non-destructive-toggle philosophy elsewhere (e.g.
    /// RoutineReminders surviving the Reminders master switch being turned off).</summary>
    public List<Guid> MyRigIds { get; set; } = new();

    public ReminderSettings Reminders { get; set; } = new();

    /// <summary>
    /// A member's own reminder for a routine they use, keyed by RoutineId —
    /// independent of who owns the routine definition or which day/slot it's
    /// scheduled under, and independent of the shared Reminders master switch.
    /// Presence in this dictionary means "I've set up a reminder for this
    /// routine"; absence means no reminder, regardless of Reminders.Enabled.
    /// Turning the master switch off never removes these entries — it only
    /// stops anything from actually being scheduled — so re-enabling it brings
    /// every per-routine reminder straight back without the member re-entering
    /// anything. See IWorkoutReminderService.
    /// </summary>
    public Dictionary<Guid, RoutineReminderSettings> RoutineReminders { get; set; } = new();

    public List<LoggedMeal> Meals { get; set; } = new();
    public MacroGoals MacroGoals { get; set; } = new();
    public List<BodyMeasurementEntry> Measurements { get; set; } = new();
    public List<CustomMeasurementSlot> CustomMeasurementSlots { get; set; } = new();
    public List<ProgressPhotoEntry> ProgressPhotos { get; set; } = new();

    public List<SupplementStack> Stacks { get; set; } = new();
    public List<StackLogEntry> StackLog { get; set; } = new();

    public List<NotificationEntry> Notifications { get; set; } = new();

    /// <summary>
    /// Empty means "never customized" — Home falls back to a synthesized
    /// default (every known module, enabled, in a fixed order) rather than
    /// showing a blank dashboard. Only written once the member actually opens
    /// Customize Home and saves.
    /// </summary>
    public List<HomeModuleConfig> HomeModules { get; set; } = new();

    /// <summary>Whole-document last-write-wins timestamp — see AccountIndex.UpdatedAt for the same caveat.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// One module's presence/position on the Home dashboard. SummaryVariant is a
/// forward-looking hook — every module has exactly one variant today ("default"),
/// but the field exists now so adding alternate summaries later (a chart instead
/// of a list, say) doesn't need a data migration.
/// </summary>
public class HomeModuleConfig
{
    public string ModuleId { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public int Order { get; set; }
    public string SummaryVariant { get; set; } = "default";
}

/// <summary>One weigh-in/measurement for one date — at most one per date, upserted by date. Values are in whatever unit the member's WeightUnit/measurement convention already implies elsewhere (lbs/kg for weight, inches for circumference).</summary>
public class BodyMeasurementEntry
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public double? Weight { get; set; }
    public double? Neck { get; set; }
    public double? Shoulder { get; set; }
    public double? Chest { get; set; }
    public double? LBicep { get; set; }
    public double? RBicep { get; set; }
    public double? Waist { get; set; }
    public double? Abdomen { get; set; }
    public double? Hip { get; set; }
    public double? LThigh { get; set; }
    public double? RThigh { get; set; }
    public double? LCalf { get; set; }
    public double? RCalf { get; set; }

    /// <summary>Values for this member's own CustomMeasurementSlots, keyed by slot id.</summary>
    public Dictionary<Guid, double> CustomValues { get; set; } = new();

    public string? Notes { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>A member-defined measurement location beyond the built-in list (e.g. "Forearm") — tracked the same way as any built-in field, via BodyMeasurementEntry.CustomValues.</summary>
public class CustomMeasurementSlot
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>Master on/off switch for every per-routine reminder — see IWorkoutReminderService and MemberData.RoutineReminders.</summary>
public class ReminderSettings
{
    public bool Enabled { get; set; }
}

/// <summary>One member's personal reminder for one routine. See MemberData.RoutineReminders.</summary>
public class RoutineReminderSettings
{
    public bool Enabled { get; set; } = true;
    public TimeOnly ReminderTime { get; set; } = new(7, 0);
    public bool RepeatWeekly { get; set; } = true;
    public bool SnoozeEnabled { get; set; } = true;
    public int SnoozeMinutes { get; set; } = 10;
}

public class HiitSettings
{
    public bool EndBeepEnabled { get; set; } = true;
    public bool VoiceEnabled { get; set; } = true;
    public string? VoiceUri { get; set; }
    public int Volume { get; set; } = 100;
    public bool Muted { get; set; }
}
