using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.Services;

/// <summary>
/// First-run seed data — the global manufacturer library (exercises/foods/recipes
/// shipped with the app) only. Account/member creation is real now (sign in via
/// ProfileGateViewModel), not seeded.
/// </summary>
public interface IAppBootstrapper
{
    Task EnsureSeedDataAsync();
}

public class AppBootstrapper : IAppBootstrapper
{
    private readonly IWorkoutRepository _repo;

    public AppBootstrapper(IWorkoutRepository repo) => _repo = repo;

    public async Task EnsureSeedDataAsync()
    {
        // Independent of account existence — the manufacturer library is global,
        // shipped content, not something tied to any one family's data.
        var manufacturer = await _repo.GetManufacturerLibraryAsync();
        var manufacturerChanged = false;
        if (manufacturer.Exercises.Count == 0)
        {
            var seeded = ManufacturerSeedData.Build();
            manufacturer.Exercises = seeded.Exercises;
            manufacturer.Routines = seeded.Routines;
            manufacturerChanged = true;
        }
        // Additive by name rather than "only if empty" — an account whose
        // manufacturer library was already seeded before a later app update
        // added more foods (e.g. ones a new seed recipe needs) must still pick
        // those up, or BuildRecipes below throws looking for an ingredient
        // that was never backfilled in and this whole await hangs forever.
        var existingFoodNames = manufacturer.Foods.Select(f => f.Name).ToHashSet();
        var newFoods = NutritionSeedData.BuildFoods().Where(f => !existingFoodNames.Contains(f.Name)).ToList();
        if (newFoods.Count > 0)
        {
            manufacturer.Foods.AddRange(newFoods);
            manufacturerChanged = true;
        }

        var existingRecipeNames = manufacturer.Recipes.Select(r => r.Name).ToHashSet();
        var newRecipes = NutritionSeedData.BuildRecipes(manufacturer.Foods).Where(r => !existingRecipeNames.Contains(r.Name)).ToList();
        if (newRecipes.Count > 0)
        {
            manufacturer.Recipes.AddRange(newRecipes);
            manufacturerChanged = true;
        }
        if (manufacturerChanged)
        {
            await _repo.SaveManufacturerLibraryAsync(manufacturer);
        }

        // Same additive-by-name approach as Foods/Recipes above, in its own
        // document since Rigs live in a separate library/rigs.json file.
        var rigCatalog = await _repo.GetRigCatalogAsync();
        var existingRigNames = rigCatalog.Rigs.Select(r => r.Name).ToHashSet();
        var newRigs = RigSeedData.Build().Rigs.Where(r => !existingRigNames.Contains(r.Name)).ToList();
        if (newRigs.Count > 0)
        {
            rigCatalog.Rigs.AddRange(newRigs);
            await _repo.SaveRigCatalogAsync(rigCatalog);
        }

        // No demo Account/Member seeding anymore — sign-in (Google/email, via
        // ProfileGateViewModel + Firebase) is now the real way an account/member
        // comes to exist, so a device legitimately has zero accounts until someone
        // actually signs in on it. The manufacturer library above still seeds
        // unconditionally since it's global, unauthenticated, shared content.
    }
}
