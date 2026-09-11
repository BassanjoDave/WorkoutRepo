using WorkoutTracker.Models;

namespace WorkoutTracker.ViewModels;

/// <summary>One serving's full macro/micro profile — the shape shared by FoodItem, a scaled recipe, and a logged entry.</summary>
public record FoodMacros(double Calories, double ProteinG, double CarbsG, double FatG, double SodiumMg, double FiberG, double SugarG, double SugarAlcoholG)
{
    public static readonly FoodMacros Zero = new(0, 0, 0, 0, 0, 0, 0, 0);

    public static FoodMacros From(FoodItem food) =>
        new(food.Calories, food.ProteinG, food.CarbsG, food.FatG, food.SodiumMg, food.FiberG, food.SugarG, food.SugarAlcoholG);

    public FoodMacros Scale(double quantity) => new(
        Calories * quantity, ProteinG * quantity, CarbsG * quantity, FatG * quantity,
        SodiumMg * quantity, FiberG * quantity, SugarG * quantity, SugarAlcoholG * quantity);
}

/// <summary>Per-serving macro math shared by the food picker, recipe builder preview, and food library list.</summary>
public static class NutritionMath
{
    /// <summary>
    /// How many of the food's own servings one recipe ingredient amount comes
    /// out to — e.g. a food served "1 cup" and an ingredient of "2 tbsp" gives
    /// 2 tbsp / (1 cup = 16 tbsp) = 0.125. Falls back to treating Quantity as a
    /// plain multiplier of the food's serving when the units aren't in a
    /// convertible family (e.g. the food's serving is in grams but the
    /// ingredient is in cups) — there's no ingredient density to bridge that.
    /// </summary>
    public static double ServingsFor(FoodItem food, RecipeIngredient ingredient)
    {
        var (servingQuantity, servingUnit) = ServingLabelParser.Parse(food.ServingLabel);
        var converted = UnitConversion.Convert(ingredient.Quantity, ingredient.Unit, servingUnit);
        return converted is double c && servingQuantity > 0 ? c / servingQuantity : ingredient.Quantity;
    }

    public static FoodMacros PerServing(Recipe recipe, IEnumerable<FoodItem> allFoods)
    {
        var foods = allFoods as IReadOnlyCollection<FoodItem> ?? allFoods.ToList();
        var total = FoodMacros.Zero;
        foreach (var ingredient in recipe.Ingredients)
        {
            var food = foods.FirstOrDefault(f => f.Id == ingredient.FoodItemId);
            if (food is null) continue;
            var scaled = FoodMacros.From(food).Scale(ServingsFor(food, ingredient));
            total = new FoodMacros(
                total.Calories + scaled.Calories, total.ProteinG + scaled.ProteinG, total.CarbsG + scaled.CarbsG, total.FatG + scaled.FatG,
                total.SodiumMg + scaled.SodiumMg, total.FiberG + scaled.FiberG, total.SugarG + scaled.SugarG, total.SugarAlcoholG + scaled.SugarAlcoholG);
        }
        var servings = Math.Max(1, recipe.Servings);
        return total.Scale(1.0 / servings);
    }
}
