namespace WorkoutTracker.Models;

/// <summary>
/// Reference/marketing metadata for one specific commercial machine, wrapping
/// an existing equipment key (see EquipmentCatalog.KeyFor) rather than adding
/// a parallel tagging axis on Exercise/RoutineDefinition — "tailored content
/// for this Rig" is just exercises/routines whose equipment key matches.
/// </summary>
public class Rig
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public string? Description { get; set; }

    /// <summary>EquipmentCatalog.KeyFor() format — an ExerciseEquipment enum name, or "custom:{guid}".</summary>
    public string EquipmentKey { get; set; } = "";

    /// <summary>Null means no PDF link is shown for this Rig.</summary>
    public string? PdfUrl { get; set; }

    /// <summary>Null means no affiliate/purchase link is shown for this Rig.</summary>
    public string? AffiliateUrl { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
