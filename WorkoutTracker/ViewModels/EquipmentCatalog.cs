using WorkoutTracker.Models;
using Visibility = WorkoutTracker.Models.Visibility;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// Maps built-in ExerciseEquipment values (and user-added CustomEquipmentType
/// entries) onto the folder-tree main categories shown on the equipment
/// preferences screen, and resolves a member's LibraryEquipmentFilter against
/// a given exercise.
/// </summary>
public static class EquipmentCatalog
{
    public const string CustomKeyPrefix = "custom:";

    public static EquipmentMainCategory MainCategoryOf(ExerciseEquipment eq) => eq switch
    {
        ExerciseEquipment.Dumbbell or ExerciseEquipment.Barbell or ExerciseEquipment.WeightBench
            or ExerciseEquipment.Kettlebell or ExerciseEquipment.ResistanceBand => EquipmentMainCategory.FreeWeights,
        ExerciseEquipment.Bodyweight => EquipmentMainCategory.Bodyweight,
        ExerciseEquipment.CardioMachine => EquipmentMainCategory.Cardio,
        ExerciseEquipment.BowflexMachine => EquipmentMainCategory.Manufacturer,
        ExerciseEquipment.VibrationPlate => EquipmentMainCategory.VibrationPlate,
        _ => EquipmentMainCategory.Other,
    };

    public static string Label(ExerciseEquipment eq) => eq switch
    {
        ExerciseEquipment.BowflexMachine => "Bowflex Rig",
        ExerciseEquipment.VibrationPlate => "Vibration Plate",
        ExerciseEquipment.CardioMachine => "Cardio Rig",
        ExerciseEquipment.WeightBench => "Weight Bench",
        ExerciseEquipment.ResistanceBand => "Resistance Band",
        _ => eq.ToString(),
    };

    public static string MainCategoryLabel(EquipmentMainCategory c) => c switch
    {
        EquipmentMainCategory.FreeWeights => "Free Weights",
        EquipmentMainCategory.VibrationPlate => "Vibration Plate",
        _ => c.ToString(),
    };

    public static string KeyFor(ExerciseEquipment eq) => eq.ToString();
    public static string KeyFor(CustomEquipmentType custom) => CustomKeyPrefix + custom.Id;

    /// <summary>The specific machine an exercise runs on (e.g. "Bowflex Xceed"), looked
    /// up from the real Rig catalog by equipment key — falls back to the equipment
    /// type's generic label (e.g. "Kettlebell") when there's no matching Rig entry.
    /// This is what distinguishes two same-named exercises on different equipment
    /// (e.g. "Biceps Curl" on a Bowflex vs. as a Dumbbell exercise).</summary>
    public static string MachineLabel(Exercise e, IEnumerable<Rig> rigs) =>
        e.Visibility == Visibility.Manufacturer
            ? rigs.FirstOrDefault(r => r.EquipmentKey == KeyFor(e))?.Name ?? Label(e.Equipment)
            : Label(e.Equipment);

    /// <summary>Resolves an exercise's actual equipment key, handling the Custom case the same way IsExerciseEnabled does.</summary>
    public static string KeyFor(Exercise e) =>
        e.Equipment == ExerciseEquipment.Custom && e.CustomEquipmentTypeId is Guid customId
            ? CustomKeyPrefix + customId
            : KeyFor(e.Equipment);

    /// <summary>Null filter = not customized yet, so everything shows.</summary>
    public static bool IsExerciseEnabled(List<string>? filter, Exercise e)
    {
        if (filter is null) return true;
        if (e.Equipment == ExerciseEquipment.Custom && e.CustomEquipmentTypeId is Guid customId)
        {
            return filter.Contains(CustomKeyPrefix + customId);
        }
        return filter.Contains(KeyFor(e.Equipment));
    }
}
