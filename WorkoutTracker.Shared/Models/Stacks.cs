namespace WorkoutTracker.Models;

/// <summary>Allowed values for StackItem.Kind/Form/DoseUnit — plain strings rather
/// than enums, matching HiitSection.Type's own convention in this codebase, so the
/// list can grow without a data migration.</summary>
public static class StackOptions
{
    public static readonly string[] Kinds = { "Supplement", "Medication" };

    public static readonly string[] Forms =
    {
        "Pill", "Tablet", "Capsule", "Softgel", "Chewable", "Gummy", "Powder", "Liquid",
        "Drops", "Spray", "Inhalant", "Patch", "Topical/Cream", "Suppository", "Sublingual",
        "Subdermal Injection", "Intramuscular Injection", "Intravenous Injection", "Other",
    };

    public static readonly string[] DoseUnits = { "mg", "mcg", "g", "mL", "IU", "%", "other" };

    /// <summary>The countable unit QuantityTaken is measured in — e.g. "2 Softgels",
    /// "1 Tablespoon". Deliberately separate from Forms: Form describes what the item
    /// physically is, TakenUnit describes how this particular dose is counted (mostly
    /// the same word, but "Puff"/"Spray"/"Drop" etc. read naturally here even for an
    /// item whose Form is "Inhalant"/"Liquid"). Stored singular; StackItemRowViewModel
    /// pluralizes for display based on the actual quantity.</summary>
    public static readonly string[] TakenUnits =
    {
        "Pill", "Tablet", "Capsule", "Softgel", "Gummy", "Chewable", "Lozenge",
        "Tablespoon", "Teaspoon", "Gram", "Milliliter", "Ounce", "Unit",
        "Puff", "Spray", "Drop", "Patch", "Injection", "Application", "Other",
    };
}

/// <summary>
/// A group of supplements/medications taken together at the same time of day, on a
/// chosen set of weekdays — the daily-dose equivalent of a HIIT routine (named,
/// scheduled, reminder-capable), but with its own absolute time-of-day rather than
/// a workout's AM/PM slot. See StackItem for what's actually in it, StackLogEntry
/// for adherence tracking, and IStackReminderService for how the reminder fires.
/// </summary>
public class SupplementStack
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";

    /// <summary>Empty means "every day" — the builder's "All" toggle just fills every Weekday in here rather than needing a separate flag.</summary>
    public HashSet<Weekday> Days { get; set; } = new();
    public TimeOnly Time { get; set; } = new(8, 0);
    public List<StackItem> Items { get; set; } = new();

    public bool ReminderEnabled { get; set; }
    public bool SnoozeEnabled { get; set; } = true;
    public int SnoozeMinutes { get; set; } = 10;

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>One supplement or medication within a stack.</summary>
public class StackItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "Supplement"; // StackOptions.Kinds
    public string Form { get; set; } = "Pill"; // StackOptions.Forms

    /// <summary>Strength per unit, e.g. 500 for "500 mg" — paired with DoseUnit.</summary>
    public double DoseQuantity { get; set; }
    public string DoseUnit { get; set; } = "mg"; // StackOptions.DoseUnits

    /// <summary>How many of TakenUnit are taken at once, e.g. 2 (softgels).</summary>
    public double QuantityTaken { get; set; } = 1;
    public string TakenUnit { get; set; } = "Pill"; // StackOptions.TakenUnits

    public string Note { get; set; } = "";
}

/// <summary>One (stack, date) adherence record — at most one per stack per date, upserted by date.</summary>
public class StackLogEntry
{
    public Guid StackId { get; set; }
    public DateOnly Date { get; set; }
    public bool Taken { get; set; }
    public DateTimeOffset? TakenAt { get; set; }
}
