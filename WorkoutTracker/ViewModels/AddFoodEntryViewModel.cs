using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.ViewModels;

/// <summary>Adds one logged entry — either a library food/recipe scaled by quantity, or a one-off custom entry not saved to the library.</summary>
public partial class AddFoodEntryViewModel : ObservableObject, IQueryAttributable
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;

    private Guid _accountId;
    private Guid _memberId;
    private MealType _mealType;
    private MemberData _memberData = new();
    private List<FoodPickerOption> _allOptions = new();

    [ObservableProperty] public partial string MealTypeLabel { get; set; } = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowQuantityFooter))]
    public partial bool IsCustomEntry { get; set; }
    [ObservableProperty] public partial bool ShowFoods { get; set; } = true;
    [ObservableProperty] public partial bool ShowRecipes { get; set; } = true;
    [ObservableProperty] public partial string SearchText { get; set; } = "";
    [ObservableProperty] public partial ObservableCollection<FoodPickerOption> FilteredOptions { get; set; } = new();
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowQuantityFooter))]
    public partial FoodPickerOption? SelectedOption { get; set; }
    [ObservableProperty] public partial string Quantity { get; set; } = "1";

    /// <summary>Shows the servings box in the footer, next to Add, once a library
    /// item is actually picked — keeps it out of the scrollable list area (was
    /// easy to miss below a long list) and right where it's needed at save time.</summary>
    public bool ShowQuantityFooter => !IsCustomEntry && SelectedOption is not null;

    [ObservableProperty] public partial string CustomName { get; set; } = "";
    [ObservableProperty] public partial string CustomCalories { get; set; } = "";
    [ObservableProperty] public partial string CustomProtein { get; set; } = "";
    [ObservableProperty] public partial string CustomCarbs { get; set; } = "";
    [ObservableProperty] public partial string CustomFat { get; set; } = "";
    [ObservableProperty] public partial string ErrorMessage { get; set; } = "";

    public AddFoodEntryViewModel(IActiveSessionService session, IWorkoutRepository repo)
    {
        _session = session;
        _repo = repo;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query) =>
        _mealType = Enum.Parse<MealType>((string)query["mealType"]);

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
        MealTypeLabel = _mealType.ToString();

        _memberData = await _repo.GetMemberDataAsync(account.Id, member.Id);
        var shared = await _repo.GetSharedLibraryAsync(account.Id);
        var manufacturer = await _repo.GetManufacturerLibraryAsync();
        var allFoods = shared.Foods.Concat(manufacturer.Foods).ToList();

        _allOptions = allFoods
            .Select(f => new FoodPickerOption(f.Id, null, string.IsNullOrWhiteSpace(f.BrandName) ? f.Name : $"{f.BrandName} {f.Name}",
                f.ServingLabel, FoodMacros.From(f)))
            .Concat(shared.Recipes.Concat(manufacturer.Recipes).Select(r =>
                new FoodPickerOption(null, r.Id, r.Name, "1 serving", NutritionMath.PerServing(r, allFoods))))
            .OrderBy(o => o.Name)
            .ToList();

        Filter();
    }

    partial void OnSearchTextChanged(string value) => Filter();
    partial void OnIsCustomEntryChanged(bool value) => ErrorMessage = "";
    partial void OnShowFoodsChanged(bool value) => Filter();
    partial void OnShowRecipesChanged(bool value) => Filter();

    private void Filter()
    {
        FilteredOptions.Clear();
        var needle = SearchText.Trim();
        var matches = _allOptions
            .Where(o => (o.FoodId is not null && ShowFoods) || (o.RecipeId is not null && ShowRecipes))
            .Where(o => string.IsNullOrEmpty(needle) || o.Name.Contains(needle, StringComparison.OrdinalIgnoreCase));
        foreach (var option in matches) FilteredOptions.Add(option);
    }

    [RelayCommand]
    private void ToggleCustomEntry() => IsCustomEntry = !IsCustomEntry;

    [RelayCommand]
    private void ToggleShowFoods() => ShowFoods = !ShowFoods;

    [RelayCommand]
    private void ToggleShowRecipes() => ShowRecipes = !ShowRecipes;

    [RelayCommand]
    private async Task Save()
    {
        LoggedFoodEntry entry;
        if (IsCustomEntry)
        {
            if (string.IsNullOrWhiteSpace(CustomName))
            {
                ErrorMessage = "Give it a name first.";
                return;
            }
            entry = new LoggedFoodEntry
            {
                Label = CustomName.Trim(),
                Quantity = 1,
                Calories = ParseDouble(CustomCalories),
                ProteinG = ParseDouble(CustomProtein),
                CarbsG = ParseDouble(CustomCarbs),
                FatG = ParseDouble(CustomFat),
            };
        }
        else
        {
            if (SelectedOption is null)
            {
                ErrorMessage = "Pick a food or recipe first.";
                return;
            }
            var qty = ParseDouble(Quantity, 1);
            if (qty <= 0) qty = 1;
            var macros = SelectedOption.Macros.Scale(qty);
            entry = new LoggedFoodEntry
            {
                FoodItemId = SelectedOption.FoodId,
                RecipeId = SelectedOption.RecipeId,
                Quantity = qty,
                Calories = macros.Calories,
                ProteinG = macros.ProteinG,
                CarbsG = macros.CarbsG,
                FatG = macros.FatG,
                SodiumMg = macros.SodiumMg,
                FiberG = macros.FiberG,
                SugarG = macros.SugarG,
                SugarAlcoholG = macros.SugarAlcoholG,
            };
        }

        var meal = _memberData.Meals.FirstOrDefault(m => m.Date == DateOnly.FromDateTime(DateTime.Today) && m.MealType == _mealType);
        if (meal is null)
        {
            meal = new LoggedMeal { Id = Guid.NewGuid(), MemberId = _memberId, Date = DateOnly.FromDateTime(DateTime.Today), MealType = _mealType };
            _memberData.Meals.Add(meal);
        }
        meal.Entries.Add(entry);
        meal.UpdatedAt = DateTimeOffset.UtcNow;

        await _repo.SaveMemberDataAsync(_accountId, _memberId, _memberData);
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task Cancel() => await Shell.Current.GoToAsync("..");

    private static double ParseDouble(string value, double fallback = 0) => double.TryParse(value, out var d) ? d : fallback;
}

public record FoodPickerOption(Guid? FoodId, Guid? RecipeId, string Name, string ServingLabel, FoodMacros Macros)
{
    public string SubtitleLabel => $"{ServingLabel} · {Macros.Calories:0} kcal · {Macros.ProteinG:0}p / {Macros.CarbsG:0}c / {Macros.FatG:0}f";
    public override string ToString() => Name;
}
