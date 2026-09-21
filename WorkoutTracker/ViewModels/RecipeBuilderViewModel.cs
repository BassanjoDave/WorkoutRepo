using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;
using Visibility = WorkoutTracker.Models.Visibility;
// Sentry.Maui (see MauiProgram.cs) adds an implicit global `using Sentry;`,
// which otherwise collides with this app's own MeasurementUnit (vs.
// Sentry.MeasurementUnit) — same fix as the Visibility alias above.
using MeasurementUnit = WorkoutTracker.Models.MeasurementUnit;

namespace WorkoutTracker.ViewModels;

public partial class RecipeBuilderViewModel : ObservableObject, IQueryAttributable
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;
    private readonly IRecipeDraftBridge _recipeDraftBridge;

    private Guid? _editingRecipeId;
    private Guid _accountId;
    private Guid _memberId;
    private string _memberName = "";
    private SharedLibrary _shared = new();
    private ManufacturerLibrary _manufacturer = new();
    private List<FoodItem> _allFoods = new();

    [ObservableProperty] public partial string RecipeName { get; set; } = "New Recipe";
    [ObservableProperty] public partial int CategoryIndex { get; set; }
    [ObservableProperty] public partial string Instructions { get; set; } = "";
    [ObservableProperty] public partial bool IsAccountShared { get; set; }
    [ObservableProperty] public partial string ServingsText { get; set; } = "1";
    [ObservableProperty] public partial ObservableCollection<RecipeIngredientRowViewModel> Ingredients { get; set; } = new();
    [ObservableProperty] public partial ObservableCollection<FoodPickerOption> AvailableFoods { get; set; } = new();
    [ObservableProperty] public partial FoodPickerOption? SelectedFoodToAdd { get; set; }
    [ObservableProperty] public partial string PreviewLabel { get; set; } = "";
    [ObservableProperty] public partial string ErrorMessage { get; set; } = "";

    // Order matches RecipeCategory's declaration order so CategoryIndex casts straight to/from the enum.
    public string[] CategoryLabels { get; } = { "Breakfast", "Entree", "Side Dish", "Salad", "Soup", "Appetizer", "Snack", "Dessert", "Candy", "Beverage", "Condiment" };

    public RecipeBuilderViewModel(IActiveSessionService session, IWorkoutRepository repo, IRecipeDraftBridge recipeDraftBridge)
    {
        _session = session;
        _repo = repo;
        _recipeDraftBridge = recipeDraftBridge;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query) =>
        _editingRecipeId = query.TryGetValue("recipeId", out var id) ? Guid.Parse((string)id) : null;

    partial void OnServingsTextChanged(string value) => RebuildPreview();

    public async Task LoadAsync()
    {
        var account = _session.ActiveAccount;
        var member = _session.ActiveMember;
        if (account is null || member is null)
        {
            await Shell.Current.GoToAsync("//gate");
            return;
        }
        _accountId = account.Id;
        _memberId = member.Id;
        _memberName = member.DisplayName;

        _shared = await _repo.GetSharedLibraryAsync(account.Id);
        _manufacturer = await _repo.GetManufacturerLibraryAsync();
        RebuildAvailableFoods();

        // Replacing the whole collection (rather than Clear() + Add() in place) avoids
        // BindableLayout briefly seeing an empty source mid-rebuild — that transient
        // empty state crashes natively inside WinUI's own child-collection handling
        // (see the same fix in WorkoutsViewModel.Rebuild()).
        var ingredients = new ObservableCollection<RecipeIngredientRowViewModel>();
        if (_editingRecipeId is Guid recipeId)
        {
            // A recipe can be edited from a base (manufacturer) or another
            // member's copy just as easily as your own — Save() below decides
            // whether that means updating in place or forking based on who
            // actually owns whichever one is found here.
            var recipe = _shared.Recipes.FirstOrDefault(r => r.Id == recipeId) ?? _manufacturer.Recipes.FirstOrDefault(r => r.Id == recipeId);
            if (recipe is not null)
            {
                RecipeName = recipe.Name;
                CategoryIndex = (int)recipe.Category;
                Instructions = recipe.Instructions;
                ServingsText = recipe.Servings.ToString();
                IsAccountShared = recipe.Visibility == Visibility.Account;
                foreach (var ingredient in recipe.Ingredients)
                {
                    var food = _allFoods.FirstOrDefault(f => f.Id == ingredient.FoodItemId);
                    if (food is null) continue;
                    ingredients.Add(CreateIngredientRow(food, ingredient.Quantity, ingredient.Unit));
                }
            }
        }
        Ingredients = ingredients;

        RebuildPreview();
    }

    /// <summary>
    /// Called instead of LoadAsync when the page reappears after "Can't find
    /// it? Add a new food" — refreshes the food list and, if that trip actually
    /// created one, asks whether to add it as an ingredient here. Deliberately
    /// leaves the rest of the in-progress draft (name/category/instructions/
    /// existing ingredients) untouched, unlike LoadAsync which would reload
    /// everything from scratch and lose unsaved edits.
    /// </summary>
    public async Task RefreshAfterAddingFoodAsync()
    {
        _shared = await _repo.GetSharedLibraryAsync(_accountId);
        RebuildAvailableFoods();

        if (_recipeDraftBridge.ConsumePendingFoodId() is Guid newFoodId)
        {
            var food = _allFoods.FirstOrDefault(f => f.Id == newFoodId);
            if (food is not null && Shell.Current?.CurrentPage is Page page)
            {
                var add = await page.DisplayAlertAsync("Food created",
                    $"Add \"{food.Name}\" to this recipe?", "Add", "Not now");
                if (add) AddIngredientRowWithParsedServing(food);
            }
        }
        RebuildPreview();
    }

    private void RebuildAvailableFoods()
    {
        _allFoods = _shared.Foods.Concat(_manufacturer.Foods).OrderBy(f => f.Name).ToList();
        AvailableFoods.Clear();
        // Pinned first so it's seen right where the member is already looking, not
        // just as the easy-to-miss ghost button below — both trigger AddNewFood;
        // this sentinel has FoodId/RecipeId both null so nothing else can collide
        // with it (see OnSelectedFoodToAddChanged).
        AvailableFoods.Add(new FoodPickerOption(null, null, "Can't find it? + Add new food", "", FoodMacros.Zero));
        foreach (var f in _allFoods)
        {
            AvailableFoods.Add(new FoodPickerOption(f.Id, null, f.Name, f.ServingLabel, FoodMacros.From(f)));
        }
    }

    partial void OnSelectedFoodToAddChanged(FoodPickerOption? value)
    {
        if (value is null || value.FoodId is not null || value.RecipeId is not null) return;
        SelectedFoodToAdd = null;
        AddNewFoodCommand.Execute(null);
    }

    private RecipeIngredientRowViewModel CreateIngredientRow(FoodItem food, double quantity, MeasurementUnit unit)
    {
        var row = new RecipeIngredientRowViewModel(food, quantity.ToString("0.##"), unit, RemoveIngredientCommand);
        row.PropertyChanged += (_, _) => RebuildPreview();
        return row;
    }

    private void AddIngredientRow(FoodItem food, double quantity, MeasurementUnit unit) =>
        Ingredients.Add(CreateIngredientRow(food, quantity, unit));

    /// <summary>Adds an ingredient defaulting to the food's own serving amount/unit — e.g. a food served "1 cup" starts as 1 cup, not 1 gram.</summary>
    private void AddIngredientRowWithParsedServing(FoodItem food)
    {
        var (quantity, unit) = ServingLabelParser.Parse(food.ServingLabel);
        AddIngredientRow(food, quantity, unit);
    }

    [RelayCommand]
    private void AddIngredient()
    {
        if (SelectedFoodToAdd is not FoodPickerOption option || option.FoodId is not Guid foodId) return;
        var food = _allFoods.FirstOrDefault(f => f.Id == foodId);
        if (food is null) return;
        AddIngredientRowWithParsedServing(food);
        SelectedFoodToAdd = null;
        RebuildPreview();
    }

    [RelayCommand]
    private async Task AddNewFood() => await Shell.Current.GoToAsync("foodEditor?fromRecipe=true");

    [RelayCommand]
    private void RemoveIngredient(RecipeIngredientRowViewModel row)
    {
        Ingredients.Remove(row);
        RebuildPreview();
    }

    private void RebuildPreview()
    {
        var servings = Math.Max(1, ParseInt(ServingsText, 1));
        var total = FoodMacros.Zero;
        foreach (var row in Ingredients)
        {
            var scaled = FoodMacros.From(row.Food).Scale(NutritionMath.ServingsFor(row.Food, row.ToIngredient()));
            total = new FoodMacros(
                total.Calories + scaled.Calories, total.ProteinG + scaled.ProteinG, total.CarbsG + scaled.CarbsG, total.FatG + scaled.FatG,
                total.SodiumMg + scaled.SodiumMg, total.FiberG + scaled.FiberG, total.SugarG + scaled.SugarG, total.SugarAlcoholG + scaled.SugarAlcoholG);
        }
        PreviewLabel = Ingredients.Count == 0
            ? "Add ingredients to see per-serving macros."
            : $"Per serving: {total.Calories / servings:0} kcal · {total.ProteinG / servings:0}p / {total.CarbsG / servings:0}c / {total.FatG / servings:0}f";
    }

    [RelayCommand]
    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(RecipeName) || Ingredients.Count == 0)
        {
            ErrorMessage = "Give it a name and at least one ingredient.";
            return;
        }
        if (Shell.Current?.CurrentPage is not Page page) return;

        Recipe? source = _editingRecipeId is Guid rid
            ? _shared.Recipes.FirstOrDefault(r => r.Id == rid) ?? _manufacturer.Recipes.FirstOrDefault(r => r.Id == rid)
            : null;
        var isOwnedByMe = source is not null && source.OwnerMemberId == _memberId && _shared.Recipes.Contains(source);

        if (source is not null && !isOwnedByMe)
        {
            // A base recipe or another account member's — never overwrite it in
            // place, always fork under a name the member chooses.
            await SaveAsForkAsync(source, page);
            return;
        }

        var isNew = source is null;
        var target = source ?? new Recipe { Id = Guid.NewGuid(), AccountId = _accountId, OwnerMemberId = _memberId, OwnerNameSnapshot = _memberName };

        if (!isNew)
        {
            var choice = await page.DisplayActionSheetAsync("Save recipe", "Cancel", "Discard changes", "Save changes", "Save as new recipe");
            if (choice is null || choice == "Cancel") return;
            if (choice == "Discard changes")
            {
                await Shell.Current!.GoToAsync("..");
                return;
            }
            if (choice == "Save as new recipe")
            {
                await SaveAsForkAsync(source!, page);
                return;
            }
        }

        ApplyFields(target);
        if (isNew) _shared.Recipes.Add(target);
        await _repo.SaveSharedLibraryAsync(_accountId, _shared);
        await Shell.Current!.GoToAsync("..");
    }

    private async Task SaveAsForkAsync(Recipe source, Page page)
    {
        var newName = await page.DisplayPromptAsync("Save as your recipe",
            source.OwnerMemberId is null
                ? "This is a base recipe — name your copy:"
                : $"This is {source.OwnerNameSnapshot ?? "another member"}'s recipe — name your copy:",
            "Save", "Cancel", initialValue: RecipeName);
        if (string.IsNullOrWhiteSpace(newName)) return;

        var fork = new Recipe
        {
            Id = Guid.NewGuid(),
            AccountId = _accountId,
            OwnerMemberId = _memberId,
            OwnerNameSnapshot = _memberName,
            SourceRecipeId = source.Id,
            SourceNameSnapshot = source.Name,
        };
        RecipeName = newName.Trim();
        ApplyFields(fork);
        _shared.Recipes.Add(fork);
        await _repo.SaveSharedLibraryAsync(_accountId, _shared);
        await Shell.Current!.GoToAsync("..");
    }

    private void ApplyFields(Recipe target)
    {
        target.Name = RecipeName.Trim();
        target.Category = (RecipeCategory)CategoryIndex;
        target.Instructions = Instructions.Trim();
        target.Servings = Math.Max(1, ParseInt(ServingsText, 1));
        target.Ingredients = Ingredients.Select(row => row.ToIngredient()).ToList();
        target.Visibility = IsAccountShared ? Visibility.Account : Visibility.Private;
        target.UpdatedAt = DateTimeOffset.UtcNow;
    }

    [RelayCommand]
    private async Task Cancel() => await Shell.Current.GoToAsync("..");

    private static int ParseInt(string value, int fallback = 0) => int.TryParse(value, out var i) ? i : fallback;
}

public partial class RecipeIngredientRowViewModel : ObservableObject
{
    public FoodItem Food { get; }
    public Guid FoodId => Food.Id;
    public string Name => Food.Name;
    public IRelayCommand<RecipeIngredientRowViewModel> RemoveCommand { get; }

    [ObservableProperty] public partial string QuantityText { get; set; }
    [ObservableProperty] public partial int UnitIndex { get; set; }

    // Order matches MeasurementUnit's declaration order so UnitIndex casts straight to/from the enum.
    public string[] UnitLabels { get; } = { "g", "oz", "lb", "ml", "tsp", "tbsp", "cup", "whole" };

    private MeasurementUnit _previousUnit;

    public RecipeIngredientRowViewModel(FoodItem food, string quantityText, MeasurementUnit unit, IRelayCommand<RecipeIngredientRowViewModel> removeCommand)
    {
        Food = food;
        QuantityText = quantityText;
        RemoveCommand = removeCommand;
        _previousUnit = unit;
        UnitIndex = (int)unit;
    }

    // Changing units converts the number too (1 cup -> 16 tbsp) when they're
    // in the same convertible family, so switching units feels like real
    // cooking-unit entry rather than resetting the amount to something wrong.
    partial void OnUnitIndexChanged(int value)
    {
        var newUnit = (MeasurementUnit)value;
        if (double.TryParse(QuantityText, out var qty) && UnitConversion.Convert(qty, _previousUnit, newUnit) is double converted)
        {
            QuantityText = converted.ToString("0.##");
        }
        _previousUnit = newUnit;
    }

    public RecipeIngredient ToIngredient() => new()
    {
        FoodItemId = Food.Id,
        Quantity = double.TryParse(QuantityText, out var q) ? q : 1,
        Unit = (MeasurementUnit)UnitIndex,
    };
}
