namespace WorkoutTracker.Models;

/// <summary>
/// The containers that map 1:1 onto the repository's storage documents
/// (one JSON file each, whichever backend implements IWorkoutRepository).
/// See IWorkoutRepository for the layout these correspond to.
/// </summary>

/// <summary>Root document: which accounts this device knows about, and who's active.</summary>
public class DeviceIndex
{
    public List<Guid> AccountIds { get; set; } = new();
    public Guid? ActiveAccountId { get; set; }
    public Guid? ActiveMemberId { get; set; }
}

/// <summary>accounts/{accountId}/index.json — the account's own metadata plus its member roster.</summary>
public class AccountIndex
{
    public Account Account { get; set; } = new();
    public List<Member> Members { get; set; } = new();

    /// <summary>
    /// Real Google-account links for this account's members. Simplification:
    /// Identity.md says one Identity can span multiple accounts (a teen who's
    /// both a family member and their own holder), but that cross-account
    /// case has no storage story yet — for now an Identity is only ever
    /// looked up within the account of the member linking it.
    /// </summary>
    public List<Identity> Identities { get; set; } = new();

    /// <summary>
    /// Whole-document timestamp used for last-write-wins sync conflict
    /// resolution. This is coarser than per-field merging — two devices
    /// editing different members while both offline will have one edit
    /// silently lose — but it's an honest, simple starting point rather than
    /// pretending to solve concurrent field-level merges that aren't built yet.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>accounts/{accountId}/shared.json — Visibility.Account content, readable by every member of the account.</summary>
public class SharedLibrary
{
    public List<RoutineDefinition> Routines { get; set; } = new();
    public List<Exercise> Exercises { get; set; } = new();
    public List<CustomEquipmentType> CustomEquipmentTypes { get; set; } = new();
    public List<FoodItem> Foods { get; set; } = new();
    public List<Recipe> Recipes { get; set; } = new();

    /// <summary>Whole-document last-write-wins timestamp — see AccountIndex.UpdatedAt for the same caveat.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>library/manufacturer.json — global, read-only, not account-scoped.</summary>
public class ManufacturerLibrary
{
    public List<Exercise> Exercises { get; set; } = new();
    public List<RoutineDefinition> Routines { get; set; } = new();
    public List<FoodItem> Foods { get; set; } = new();
    public List<Recipe> Recipes { get; set; } = new();
}

/// <summary>library/rigs.json — global, read-only catalog of commercial machines, not account-scoped.</summary>
public class RigCatalog
{
    public List<Rig> Rigs { get; set; } = new();
}
