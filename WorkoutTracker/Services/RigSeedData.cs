using WorkoutTracker.Models;

namespace WorkoutTracker.Services;

/// <summary>
/// Hand-written seed content for the global Rig catalog — unlike
/// ManufacturerSeedData.cs, this isn't auto-generated from a design handoff,
/// so it's fine to edit directly, including filling in real PdfUrl/AffiliateUrl
/// values as they become available. Entries here deliberately ship with those
/// link fields null (no legitimate source for real manufacturer/affiliate URLs
/// at seed time) to demonstrate the "no link data → no link shown" behavior;
/// see IAppBootstrapper.EnsureSeedDataAsync for how this gets applied
/// additively (by Name) so a later app update can add more Rigs without
/// duplicating or clobbering ones a member has already added.
/// </summary>
public static class RigSeedData
{
    private static readonly DateTimeOffset SeedTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static RigCatalog Build()
    {
        var catalog = new RigCatalog();

        catalog.Rigs.Add(new Rig
        {
            Id = Guid.Parse("2a6e7b6a-2c37-5a3d-9c9a-9e6a2f4a8b31"),
            Name = "Bowflex Xceed",
            Manufacturer = "Bowflex",
            Description = "Home gym with Power Rod resistance — the machine behind this app's built-in strength exercise library.",
            EquipmentKey = "BowflexMachine",
            PdfUrl = null,
            AffiliateUrl = null,
            UpdatedAt = SeedTime,
        });

        catalog.Rigs.Add(new Rig
        {
            Id = Guid.Parse("6f9b6a0e-3d7f-5a10-8e7a-8b0f6f2a1c44"),
            Name = "Vibration Plate",
            Manufacturer = "Generic",
            Description = "Whole-body vibration platform used for the app's vibration-plate exercise library.",
            EquipmentKey = "VibrationPlate",
            PdfUrl = null,
            AffiliateUrl = null,
            UpdatedAt = SeedTime,
        });

        return catalog;
    }
}
