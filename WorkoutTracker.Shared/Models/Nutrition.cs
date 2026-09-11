namespace WorkoutTracker.Models;

public enum MealType { Breakfast, Lunch, Dinner, Snack }

/// <summary>
/// The definition — a food's macro profile per one serving. Manufacturer/seed
/// foods are global (AccountId/OwnerMemberId null); member-added foods are
/// scoped to an account like Exercise. Logging a food snapshots its macros
/// onto LoggedFoodEntry, so editing a FoodItem later never rewrites history —
/// same relationship as RoutineDefinition to WorkoutSession.
/// </summary>
public class FoodItem
{
    public Guid Id { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? OwnerMemberId { get; set; }
    public string Name { get; set; } = "";
    public string? BrandName { get; set; }
    public string ServingLabel { get; set; } = "";
    public double Calories { get; set; }
    public double ProteinG { get; set; }
    public double CarbsG { get; set; }
    public double FatG { get; set; }
    public double SodiumMg { get; set; }
    public double FiberG { get; set; }
    public double SugarG { get; set; }
    public double SugarAlcoholG { get; set; }
    public Visibility Visibility { get; set; } = Visibility.Manufacturer;
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>A named combination of FoodItems, scaled to a serving count. Per-serving macros are computed from the current ingredient list, not stored.</summary>
public class Recipe
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }

    /// <summary>Null for manufacturer-seeded ("base") recipes.</summary>
    public Guid? OwnerMemberId { get; set; }
    public string? OwnerNameSnapshot { get; set; }

    /// <summary>Set when editing a recipe you don't own (a base recipe, or another account member's) forked this one from it.</summary>
    public Guid? SourceRecipeId { get; set; }
    public string? SourceNameSnapshot { get; set; }

    public string Name { get; set; } = "";
    public RecipeCategory Category { get; set; } = RecipeCategory.Entree;
    public string Instructions { get; set; } = "";
    public int Servings { get; set; } = 1;
    public List<RecipeIngredient> Ingredients { get; set; } = new();
    public Visibility Visibility { get; set; } = Visibility.Private;
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

public class RecipeIngredient
{
    public Guid FoodItemId { get; set; }

    /// <summary>A real cooking amount ("1 cup", "2 tbsp") in Unit — not a multiplier of the food's serving. See UnitConversion/ServingLabelParser for how this scales against the food's own (free-text) ServingLabel.</summary>
    public double Quantity { get; set; } = 1;
    public MeasurementUnit Unit { get; set; } = MeasurementUnit.Whole;
}

/// <summary>
/// One member's logged meal for one date — the instance/log, mirroring
/// WorkoutSession's relationship to RoutineDefinition.
/// </summary>
public class LoggedMeal
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public DateOnly Date { get; set; }
    public MealType MealType { get; set; }
    public List<LoggedFoodEntry> Entries { get; set; } = new();
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Macro fields are a snapshot taken at logging time — see FoodItem's doc comment for why.</summary>
public class LoggedFoodEntry
{
    public Guid? FoodItemId { get; set; }
    public Guid? RecipeId { get; set; }

    /// <summary>Set only for a one-off entry not backed by a library FoodItem/Recipe.</summary>
    public string? Label { get; set; }

    public double Quantity { get; set; } = 1;
    public double Calories { get; set; }
    public double ProteinG { get; set; }
    public double CarbsG { get; set; }
    public double FatG { get; set; }
    public double SodiumMg { get; set; }
    public double FiberG { get; set; }
    public double SugarG { get; set; }
    public double SugarAlcoholG { get; set; }
}

public class MacroGoals
{
    public double Calories { get; set; } = 2000;
    public double ProteinG { get; set; } = 150;
    public double CarbsG { get; set; } = 200;
    public double FatG { get; set; } = 65;

    /// <summary>Which of today's running totals show on the Home dashboard's Nutrition card. All false means the card shows nothing (Home hides it).</summary>
    public bool ShowCaloriesOnHome { get; set; } = true;
    public bool ShowProteinOnHome { get; set; }
    public bool ShowCarbsOnHome { get; set; }
    public bool ShowFatOnHome { get; set; }
}
