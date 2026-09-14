namespace WorkoutTracker.Models;

public enum RolePreset { Owner, Adult, Teen, Child, Guest }

public enum DeviceAuthMode { None, Pin, Biometric }

public enum MemberStatus { Active, Removed }

/// <summary>
/// Private: owner only. Account: shared with the owner's current account.
/// Community: cross-account sharing, reserved for a later release — no UI reads
/// or writes this value yet, it only has to survive round-tripping through storage.
/// Manufacturer: read-only seed content, OwnerMemberId is always null.
/// </summary>
public enum Visibility { Private, Account, Community, Manufacturer }

public enum RoutineType { Standard, Hiit }

public enum RecipeCategory { Breakfast, Entree, SideDish, Salad, Soup, Appetizer, Snack, Dessert, Candy, Beverage, Condiment }

/// <summary>
/// A recipe ingredient's real cooking unit — "1 cup flour", "1 tsp baking
/// powder" — as opposed to "servings of that food." Gram/Ounce/Pound convert
/// to each other (fixed weight ratios); Milliliter/Teaspoon/Tablespoon/Cup
/// convert to each other (fixed volume ratios); Whole doesn't convert to
/// anything else. Weight <-> volume (cups -> grams) isn't convertible here —
/// that needs an ingredient's density, which this doesn't model.
/// </summary>
public enum MeasurementUnit { Gram, Ounce, Pound, Milliliter, Teaspoon, Tablespoon, Cup, Whole }

public enum Slot { Am, Pm }

public enum SessionStatus { InProgress, Completed }

public enum ExerciseEquipment { BowflexMachine, Bodyweight, Kettlebell, VibrationPlate, Stretch, CardioMachine, Custom, Dumbbell, Barbell, WeightBench, ResistanceBand }

public enum ExerciseCategory { Chest, Shoulders, Back, Arms, Abs, Legs, FullBody, Cardio, VibrationPlate }

/// <summary>The folder-tree grouping used by the Library's equipment preferences screen.</summary>
public enum EquipmentMainCategory { FreeWeights, Bodyweight, Cardio, Manufacturer, VibrationPlate, Other }
