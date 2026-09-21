using System.Globalization;
using System.Text.RegularExpressions;
using WorkoutTracker.Models;
// Sentry.Maui (see MauiProgram.cs) adds an implicit global `using Sentry;`,
// which otherwise collides with this app's own MeasurementUnit (vs.
// Sentry.MeasurementUnit) — same fix as RecipeBuilderViewModel.cs's Visibility alias.
using MeasurementUnit = WorkoutTracker.Models.MeasurementUnit;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// Fixed-ratio conversion between real cooking units within the same family
/// (weight: g/oz/lb; volume: ml/tsp/tbsp/cup). Deliberately does not attempt
/// weight&lt;-&gt;volume conversion (cups -&gt; grams) — that depends on the specific
/// ingredient's density, which nothing here models.
/// </summary>
public static class UnitConversion
{
    // Grams per unit, for the weight family; milliliters per unit, for the volume family.
    private static readonly Dictionary<MeasurementUnit, double> ToBase = new()
    {
        [MeasurementUnit.Gram] = 1.0,
        [MeasurementUnit.Ounce] = 28.3495,
        [MeasurementUnit.Pound] = 453.592,
        [MeasurementUnit.Milliliter] = 1.0,
        [MeasurementUnit.Teaspoon] = 4.92892,
        [MeasurementUnit.Tablespoon] = 14.7868,
        [MeasurementUnit.Cup] = 236.588,
        [MeasurementUnit.Whole] = 1.0,
    };

    private static bool IsWeight(MeasurementUnit u) => u is MeasurementUnit.Gram or MeasurementUnit.Ounce or MeasurementUnit.Pound;
    private static bool IsVolume(MeasurementUnit u) => u is MeasurementUnit.Milliliter or MeasurementUnit.Teaspoon or MeasurementUnit.Tablespoon or MeasurementUnit.Cup;

    public static bool SameFamily(MeasurementUnit a, MeasurementUnit b) =>
        a == b || (IsWeight(a) && IsWeight(b)) || (IsVolume(a) && IsVolume(b));

    /// <summary>Converts a quantity between units in the same convertible family, or returns null if they aren't (e.g. Cup -> Gram).</summary>
    public static double? Convert(double quantity, MeasurementUnit from, MeasurementUnit to)
    {
        if (from == to) return quantity;
        if (from == MeasurementUnit.Whole || to == MeasurementUnit.Whole) return null;
        if (!SameFamily(from, to)) return null;
        return quantity * ToBase[from] / ToBase[to];
    }

    public static string Abbreviation(MeasurementUnit u) => u switch
    {
        MeasurementUnit.Gram => "g",
        MeasurementUnit.Ounce => "oz",
        MeasurementUnit.Pound => "lb",
        MeasurementUnit.Milliliter => "ml",
        MeasurementUnit.Teaspoon => "tsp",
        MeasurementUnit.Tablespoon => "tbsp",
        MeasurementUnit.Cup => "cup",
        _ => "",
    };
}

/// <summary>
/// Best-effort reader of a FoodItem's free-text ServingLabel ("100 g", "1 cup",
/// "1/2 cup", "2 tbsp", "1 large") into a (quantity, unit) pair, so a recipe
/// ingredient's real cooking amount can be scaled against it. Anything it
/// can't recognize a unit word in — "1 large", "2 slices" — comes back as
/// MeasurementUnit.Whole, meaning "treat the whole label as one indivisible
/// unit," which matches the app's original serving-multiplier behavior.
/// </summary>
public static partial class ServingLabelParser
{
    [GeneratedRegex(@"^\s*(\d+(?:\.\d+)?|\d+\s*/\s*\d+)?\s*(.*)$")]
    private static partial Regex LeadingNumberRegex();

    public static (double Quantity, MeasurementUnit Unit) Parse(string servingLabel)
    {
        var match = LeadingNumberRegex().Match(servingLabel.Trim());
        var numberText = match.Groups[1].Value;
        var rest = match.Groups[2].Value.Trim().ToLowerInvariant();

        var quantity = string.IsNullOrEmpty(numberText) ? 1 : ParseNumber(numberText);
        var unit = rest switch
        {
            _ when rest.StartsWith("gram") || rest is "g" || rest.StartsWith("g ") => MeasurementUnit.Gram,
            _ when rest.StartsWith("ounce") || rest.StartsWith("oz") => MeasurementUnit.Ounce,
            _ when rest.StartsWith("pound") || rest.StartsWith("lb") => MeasurementUnit.Pound,
            _ when rest.StartsWith("milliliter") || rest.StartsWith("ml") => MeasurementUnit.Milliliter,
            _ when rest.StartsWith("tablespoon") || rest.StartsWith("tbsp") => MeasurementUnit.Tablespoon,
            _ when rest.StartsWith("teaspoon") || rest.StartsWith("tsp") => MeasurementUnit.Teaspoon,
            _ when rest.StartsWith("cup") => MeasurementUnit.Cup,
            _ => MeasurementUnit.Whole,
        };
        return (quantity <= 0 ? 1 : quantity, unit);
    }

    private static double ParseNumber(string text)
    {
        if (text.Contains('/'))
        {
            var parts = text.Split('/');
            if (parts.Length == 2 && double.TryParse(parts[0].Trim(), out var n) && double.TryParse(parts[1].Trim(), out var d) && d != 0)
                return n / d;
            return 1;
        }
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 1;
    }
}
