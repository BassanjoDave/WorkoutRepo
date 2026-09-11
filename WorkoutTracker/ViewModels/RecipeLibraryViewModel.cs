using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;
using Visibility = WorkoutTracker.Models.Visibility;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// Recipes grouped by category (a "tree" of expandable sections) with search
/// and three independent visibility toggles — Base (manufacturer-seeded),
/// Mine, and Others' (shared by another account member). Only ever shows this
/// account's own recipes plus base ones; a different account's recipes are
/// never loaded here at all, let alone filterable into view.
/// </summary>
public partial class RecipeLibraryViewModel : ObservableObject
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;

    private Guid _memberId;
    private List<Recipe> _allRecipes = new();
    private List<FoodItem> _allFoods = new();

    [ObservableProperty] public partial string SearchText { get; set; } = "";
    [ObservableProperty] public partial bool ShowBase { get; set; } = true;
    [ObservableProperty] public partial bool ShowMine { get; set; } = true;
    [ObservableProperty] public partial bool ShowOthers { get; set; } = true;
    [ObservableProperty] public partial ObservableCollection<RecipeCategorySectionViewModel> Sections { get; set; } = new();
    [ObservableProperty] public partial bool NoResults { get; set; }

    public RecipeLibraryViewModel(IActiveSessionService session, IWorkoutRepository repo)
    {
        _session = session;
        _repo = repo;
    }

    partial void OnSearchTextChanged(string value) => Rebuild();
    partial void OnShowBaseChanged(bool value) => Rebuild();
    partial void OnShowMineChanged(bool value) => Rebuild();
    partial void OnShowOthersChanged(bool value) => Rebuild();

    public async Task LoadAsync()
    {
        var account = _session.ActiveAccount;
        var member = _session.ActiveMember;
        if (account is null || member is null)
        {
            await Shell.Current.GoToAsync("//gate");
            return;
        }
        _memberId = member.Id;

        var shared = await _repo.GetSharedLibraryAsync(account.Id);
        var manufacturer = await _repo.GetManufacturerLibraryAsync();
        _allFoods = shared.Foods.Concat(manufacturer.Foods).ToList();

        // Account-wide sharing: anyone in the account sees each other's
        // Account-visibility recipes; Private ones show only for their owner.
        // A different account's recipes never enter this list at all.
        _allRecipes = manufacturer.Recipes
            .Concat(shared.Recipes.Where(r => r.Visibility == Visibility.Account || r.OwnerMemberId == _memberId))
            .ToList();

        Rebuild();
    }

    private void Rebuild()
    {
        var needle = SearchText.Trim();
        // Replacing the whole collection (rather than Clear() + Add() in place) avoids
        // BindableLayout briefly seeing an empty source mid-rebuild — that transient
        // empty state crashes natively inside WinUI's own child-collection handling
        // (see the same fix in WorkoutsViewModel.Rebuild()).
        var sections = new ObservableCollection<RecipeCategorySectionViewModel>();
        foreach (RecipeCategory category in Enum.GetValues<RecipeCategory>())
        {
            var rows = _allRecipes
                .Where(r => r.Category == category)
                .Where(MatchesToggles)
                .Where(r => string.IsNullOrEmpty(needle) || r.Name.Contains(needle, StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.Name)
                .Select(BuildRow)
                .ToList();
            if (rows.Count == 0) continue;
            sections.Add(new RecipeCategorySectionViewModel(CategoryLabel(category), rows));
        }
        Sections = sections;
        NoResults = Sections.Count == 0;
    }

    [RelayCommand]
    private void ToggleBaseVisible() => ShowBase = !ShowBase;

    [RelayCommand]
    private void ToggleMineVisible() => ShowMine = !ShowMine;

    [RelayCommand]
    private void ToggleOthersVisible() => ShowOthers = !ShowOthers;

    private bool MatchesToggles(Recipe r)
    {
        var isBase = r.OwnerMemberId is null;
        var isMine = r.OwnerMemberId == _memberId;
        var isOthers = !isBase && !isMine;
        return (isBase && ShowBase) || (isMine && ShowMine) || (isOthers && ShowOthers);
    }

    private RecipeRowViewModel BuildRow(Recipe r)
    {
        var macros = NutritionMath.PerServing(r, _allFoods);
        var badge = r.OwnerMemberId is null ? "Base" : r.OwnerMemberId == _memberId ? "Yours" : r.OwnerNameSnapshot ?? "Shared";
        var subtitle = $"{r.Servings} serving{(r.Servings == 1 ? "" : "s")} · {macros.Calories:0} kcal/serving · {macros.ProteinG:0}p / {macros.CarbsG:0}c / {macros.FatG:0}f";
        return new RecipeRowViewModel(r.Id, r.Name, badge, subtitle, EditCommand);
    }

    private static string CategoryLabel(RecipeCategory c) => c == RecipeCategory.SideDish ? "Side Dish" : c.ToString();

    [RelayCommand]
    private async Task Edit(Guid recipeId) => await Shell.Current.GoToAsync($"recipeBuilder?recipeId={recipeId}");

    [RelayCommand]
    private async Task AddRecipe() => await Shell.Current.GoToAsync("recipeBuilder");

    [RelayCommand]
    private async Task Back() => await Shell.Current.GoToAsync("..");
}

public partial class RecipeCategorySectionViewModel : ObservableObject
{
    public string CategoryLabel { get; }
    public List<RecipeRowViewModel> Recipes { get; }

    [ObservableProperty] public partial bool IsExpanded { get; set; } = true;

    public RecipeCategorySectionViewModel(string categoryLabel, List<RecipeRowViewModel> recipes)
    {
        CategoryLabel = categoryLabel;
        Recipes = recipes;
    }

    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;
}

public class RecipeRowViewModel
{
    public Guid Id { get; }
    public string Name { get; }
    public string Badge { get; }
    public string SubtitleLabel { get; }
    public IRelayCommand<Guid> EditCommand { get; }

    public RecipeRowViewModel(Guid id, string name, string badge, string subtitleLabel, IRelayCommand<Guid> editCommand)
    {
        Id = id;
        Name = name;
        Badge = badge;
        SubtitleLabel = subtitleLabel;
        EditCommand = editCommand;
    }
}
