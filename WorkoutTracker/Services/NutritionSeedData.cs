using WorkoutTracker.Models;
using Visibility = WorkoutTracker.Models.Visibility;

namespace WorkoutTracker.Services;

/// <summary>
/// A starter set of common whole foods with approximate per-serving macros
/// (typical USDA-ballpark values, not lab-precise) — enough to make the food
/// log usable out of the box. Members can add their own exact foods via the
/// Food Editor; a fuller database (e.g. a USDA FoodData Central import) is a
/// natural later upgrade that slots into the same FoodItem shape.
/// </summary>
public static class NutritionSeedData
{
    public static List<FoodItem> BuildFoods()
    {
        var now = DateTimeOffset.UtcNow;
        (string Name, string Serving, double Cal, double Protein, double Carbs, double Fat)[] rows =
        {
            // Proteins
            ("Chicken Breast (cooked)", "100 g", 165, 31, 0, 3.6),
            ("Ground Beef 85% Lean (cooked)", "100 g", 250, 26, 0, 17),
            ("Salmon (cooked)", "100 g", 208, 20, 0, 13),
            ("Egg", "1 large", 72, 6.3, 0.4, 4.8),
            ("Egg Whites", "1 white", 17, 3.6, 0.2, 0.1),
            ("Turkey Breast (cooked)", "100 g", 135, 30, 0, 1),
            ("Tofu (firm)", "100 g", 144, 15.8, 2.8, 8.7),
            ("Greek Yogurt (plain, nonfat)", "1 cup", 100, 17, 6, 0.5),
            ("Cottage Cheese (low-fat)", "1 cup", 163, 28, 6, 2.3),
            ("Tuna (canned in water)", "100 g", 116, 26, 0, 0.8),
            ("Shrimp (cooked)", "100 g", 99, 24, 0.2, 0.3),
            ("Whey Protein Powder", "1 scoop", 120, 24, 3, 1),
            ("Black Beans (cooked)", "1 cup", 227, 15, 41, 0.9),
            ("Lentils (cooked)", "1 cup", 230, 18, 40, 0.8),
            ("Chickpeas (cooked)", "1 cup", 269, 15, 45, 4.2),

            // Carbs / grains
            ("White Rice (cooked)", "1 cup", 205, 4.3, 45, 0.4),
            ("Brown Rice (cooked)", "1 cup", 216, 5, 45, 1.8),
            ("Oats (dry)", "1/2 cup", 150, 5, 27, 3),
            ("Quinoa (cooked)", "1 cup", 222, 8, 39, 3.6),
            ("Whole Wheat Bread", "1 slice", 69, 3.6, 12, 1),
            ("White Bread", "1 slice", 67, 2, 13, 0.9),
            ("Sweet Potato (baked)", "1 medium", 103, 2.3, 24, 0.2),
            ("Pasta (cooked)", "1 cup", 221, 8.1, 43, 1.3),
            ("Bagel", "1 medium", 245, 10, 48, 1.5),
            ("Tortilla (flour)", "1 medium", 146, 4, 24, 3.6),

            // Fruits
            ("Banana", "1 medium", 105, 1.3, 27, 0.4),
            ("Apple", "1 medium", 95, 0.5, 25, 0.3),
            ("Orange", "1 medium", 62, 1.2, 15.4, 0.2),
            ("Blueberries", "1 cup", 84, 1.1, 21, 0.5),
            ("Strawberries", "1 cup", 49, 1, 11.7, 0.5),
            ("Grapes", "1 cup", 104, 1.1, 27, 0.2),
            ("Avocado", "1/2 medium", 160, 2, 8.5, 14.7),

            // Vegetables
            ("Broccoli (cooked)", "1 cup", 55, 3.7, 11, 0.6),
            ("Spinach (raw)", "1 cup", 7, 0.9, 1.1, 0.1),
            ("Carrots (raw)", "1 cup chopped", 52, 1.2, 12, 0.3),
            ("Bell Pepper", "1 medium", 24, 1, 6, 0.2),
            ("Green Beans (cooked)", "1 cup", 44, 2.4, 10, 0.2),
            ("Mixed Salad Greens", "2 cups", 10, 0.9, 2, 0.1),

            // Dairy / fats
            ("Milk (2%)", "1 cup", 122, 8.1, 12, 4.8),
            ("Almond Milk (unsweetened)", "1 cup", 39, 1.5, 3.4, 2.9),
            ("Cheddar Cheese", "1 oz", 113, 7, 0.4, 9.3),
            ("Peanut Butter", "2 tbsp", 190, 8, 7, 16),
            ("Almonds", "1 oz", 164, 6, 6, 14),
            ("Olive Oil", "1 tbsp", 119, 0, 0, 13.5),
            ("Butter", "1 tbsp", 102, 0.1, 0, 11.5),

            // Common / prepared
            ("Protein Bar (generic)", "1 bar", 220, 20, 24, 8),
            ("Two-Egg Omelet", "1 serving", 154, 12.6, 0.8, 10.6),
            ("Coffee (black)", "1 cup", 2, 0.3, 0, 0),
            ("Honey", "1 tbsp", 64, 0.1, 17, 0),

            // Added for the starter recipes below — generically useful on their own too.
            ("Chicken Broth", "1 cup", 15, 2.2, 1, 0.5),
            ("Dark Chocolate", "1 oz", 170, 2.2, 13, 12),
            ("Balsamic Vinegar", "1 tbsp", 14, 0.1, 2.7, 0),
        };

        return rows.Select(r => new FoodItem
        {
            Id = Guid.NewGuid(),
            Name = r.Name,
            ServingLabel = r.Serving,
            Calories = r.Cal,
            ProteinG = r.Protein,
            CarbsG = r.Carbs,
            FatG = r.Fat,
            Visibility = Visibility.Manufacturer,
            UpdatedAt = now,
        }).ToList();
    }

    /// <summary>
    /// One generic starter recipe per RecipeCategory, built entirely from the
    /// foods above — call with the actual seeded list (ids only exist once
    /// BuildFoods() has run) so ingredients resolve. Members can add their own;
    /// this just means the recipe library isn't empty on first launch.
    /// </summary>
    public static List<Recipe> BuildRecipes(List<FoodItem> foods)
    {
        var now = DateTimeOffset.UtcNow;
        Guid Id(string name) => foods.First(f => f.Name == name).Id;

        (string Name, RecipeCategory Category, int Servings, string Instructions, (string Food, double Qty)[] Ingredients)[] rows =
        {
            ("Banana Oatmeal", RecipeCategory.Breakfast, 1,
                "Cook oats per package directions. Stir in sliced banana and milk.",
                new[] { ("Oats (dry)", 1.0), ("Banana", 1.0), ("Milk (2%)", 0.5) }),

            ("Chicken, Rice & Broccoli", RecipeCategory.Entree, 1,
                "Serve grilled or baked chicken breast over rice with steamed broccoli on the side.",
                new[] { ("Chicken Breast (cooked)", 1.5), ("Brown Rice (cooked)", 1.0), ("Broccoli (cooked)", 1.0) }),

            ("Buttered Broccoli", RecipeCategory.SideDish, 2,
                "Toss hot steamed broccoli with butter and a pinch of salt.",
                new[] { ("Broccoli (cooked)", 2.0), ("Butter", 1.0) }),

            ("Garden Salad", RecipeCategory.Salad, 2,
                "Toss greens, carrots, and bell pepper. Drizzle with olive oil just before serving.",
                new[] { ("Mixed Salad Greens", 2.0), ("Carrots (raw)", 1.0), ("Bell Pepper", 1.0), ("Olive Oil", 1.0) }),

            ("Chicken Vegetable Soup", RecipeCategory.Soup, 2,
                "Simmer chicken, carrots, and green beans in broth for 20 minutes.",
                new[] { ("Chicken Broth", 4.0), ("Chicken Breast (cooked)", 1.0), ("Carrots (raw)", 1.0), ("Green Beans (cooked)", 1.0) }),

            ("Chickpea Dip & Veggies", RecipeCategory.Appetizer, 4,
                "Mash chickpeas with olive oil until smooth. Serve with sliced bell pepper and carrots.",
                new[] { ("Chickpeas (cooked)", 1.0), ("Olive Oil", 1.0), ("Bell Pepper", 1.0), ("Carrots (raw)", 1.0) }),

            ("Apple & Peanut Butter", RecipeCategory.Snack, 1,
                "Slice the apple and serve with peanut butter for dipping.",
                new[] { ("Apple", 1.0), ("Peanut Butter", 1.0) }),

            ("Berry Yogurt Parfait", RecipeCategory.Dessert, 1,
                "Layer yogurt, blueberries, and a drizzle of honey in a glass.",
                new[] { ("Greek Yogurt (plain, nonfat)", 1.0), ("Blueberries", 1.0), ("Honey", 0.5) }),

            ("Chocolate Almond Bark", RecipeCategory.Candy, 4,
                "Melt chocolate, stir in almonds, spread thin on parchment, and chill until firm; break into pieces.",
                new[] { ("Dark Chocolate", 2.0), ("Almonds", 1.0) }),

            ("Berry Smoothie", RecipeCategory.Beverage, 1,
                "Blend all ingredients until smooth.",
                new[] { ("Banana", 1.0), ("Blueberries", 1.0), ("Greek Yogurt (plain, nonfat)", 0.5), ("Almond Milk (unsweetened)", 1.0) }),

            ("Balsamic Vinaigrette", RecipeCategory.Condiment, 4,
                "Whisk all ingredients together until combined.",
                new[] { ("Olive Oil", 3.0), ("Balsamic Vinegar", 1.0), ("Honey", 0.5) }),
        };

        return rows.Select(r => new Recipe
        {
            Id = Guid.NewGuid(),
            Name = r.Name,
            Category = r.Category,
            Servings = r.Servings,
            Instructions = r.Instructions,
            Ingredients = r.Ingredients.Select(i => new RecipeIngredient { FoodItemId = Id(i.Food), Quantity = i.Qty }).ToList(),
            Visibility = Visibility.Manufacturer,
            UpdatedAt = now,
        }).ToList();
    }
}
