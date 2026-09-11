namespace WorkoutTracker.Models;

public class Exercise
{
    public Guid Id { get; set; }

    /// <summary>Null for manufacturer content, which is global and not account-scoped.</summary>
    public Guid? AccountId { get; set; }

    /// <summary>Null = manufacturer seed content.</summary>
    public Guid? OwnerMemberId { get; set; }

    public string Name { get; set; } = "";
    public ExerciseCategory Category { get; set; }
    public ExerciseEquipment Equipment { get; set; }
    public string Muscles { get; set; } = "";
    public string Bench { get; set; } = "";
    public string Accessory { get; set; } = "";
    public string ArmPosition { get; set; } = "";
    public List<string> Tips { get; set; } = new();
    public Visibility Visibility { get; set; } = Visibility.Manufacturer;
    public bool IsVibrationPlate { get; set; }
    public bool IsCardio { get; set; }

    /// <summary>Set only when Equipment == Custom and the author picked a user-defined equipment/manufacturer type.</summary>
    public Guid? CustomEquipmentTypeId { get; set; }

    /// <summary>
    /// Reference photos — typically a start and finish position at minimum,
    /// more for movements that need extra angles. Ordered as authored.
    /// </summary>
    public List<ExerciseImage> Images { get; set; } = new();

    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>
/// One reference photo for an exercise. Key is either a bundled app resource
/// filename (manufacturer-supplied content, shipped with the app) or an
/// absolute local file path (a member's own photo, on-device only — not yet
/// synced across devices, same as any other local-only content in this app).
/// MAUI's ImageSource resolves either form from a single string, so display
/// code never needs to branch on IsBundled — it exists for future authoring
/// logic (e.g. members shouldn't be able to delete manufacturer photos).
/// </summary>
public class ExerciseImage
{
    public string Key { get; set; } = "";
    public bool IsBundled { get; set; }
    public string? Label { get; set; }
}

/// <summary>A user-defined equipment/manufacturer type, account-wide, added from the Library's equipment preferences screen.</summary>
public class CustomEquipmentType
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public EquipmentMainCategory MainCategory { get; set; } = EquipmentMainCategory.Other;
}

/// <summary>Vibration-plate-specific fields captured at use time, not on the exercise itself.</summary>
public class VibrationSettings
{
    public string Stance { get; set; } = "";
    public string Mode { get; set; } = "";
    public int? DurationSeconds { get; set; }
    public int? FrequencyHz { get; set; }
    public int? Level { get; set; }
}
