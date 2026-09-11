using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// Today's food log: totals vs. MacroGoals, grouped by meal type. Historical
/// browsing (like History's calendar for workouts) isn't built yet — this is
/// a "today" view for now, the natural next step once this is in daily use.
/// </summary>
public partial class NutritionViewModel : ObservableObject
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;
    private readonly IEntitlementService _entitlements;

    private Guid _accountId;
    private Guid _memberId;
    private MemberData _memberData = new();
    private SharedLibrary _shared = new();
    private ManufacturerLibrary _manufacturer = new();
    private DateOnly _today;

    [ObservableProperty] public partial string DateLabel { get; set; } = "";
    [ObservableProperty] public partial string CaloriesLabel { get; set; } = "";
    [ObservableProperty] public partial double CaloriesProgress { get; set; }
    [ObservableProperty] public partial string ProteinLabel { get; set; } = "";
    [ObservableProperty] public partial double ProteinProgress { get; set; }
    [ObservableProperty] public partial string CarbsLabel { get; set; } = "";
    [ObservableProperty] public partial double CarbsProgress { get; set; }
    [ObservableProperty] public partial string FatLabel { get; set; } = "";
    [ObservableProperty] public partial double FatProgress { get; set; }
    [ObservableProperty] public partial ObservableCollection<MealGroupViewModel> MealGroups { get; set; } = new();

    public NutritionViewModel(IActiveSessionService session, IWorkoutRepository repo, IEntitlementService entitlements)
    {
        _session = session;
        _repo = repo;
        _entitlements = entitlements;
    }

    public async Task LoadAsync()
    {
        var account = _session.ActiveAccount;
        var member = _session.ActiveMember;
        if (account is null || member is null)
        {
            await Shell.Current.GoToAsync("//gate");
            return;
        }
        if (!_entitlements.HasPageAccess(account, member.Id, PageEntitlements.Nutrition))
        {
            // Posted via InvokeOnMainThreadAsync rather than awaited inline — pushing a
            // second Shell navigation synchronously from inside OnAppearing, right behind
            // the tab-switch navigation that triggered it, crashed WinUI natively
            // (Microsoft.UI.Xaml.dll, 0xc000027b — see the project's WinUI-crash memory).
            // Posting it as a new main-thread work item lets the tab-switch transition
            // finish first. Awaited (not BeginInvokeOnMainThread's true fire-and-forget)
            // so this method doesn't return — and signal OnAppearing/LoadAsync "done" —
            // before the navigation actually completes: on Android, returning early let
            // this redirect race the still-settling incoming navigation, corrupting
            // Shell's back stack so every subsequent back action on the Upgrade page just
            // reloaded it instead of popping. Confirmed via testing: Android has no WinUI
            // crash to avoid in the first place, so this only needed to be non-blocking
            // for Windows' benefit, not literally never awaited.
            await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync($"upgrade?page={PageEntitlements.Nutrition}"));
            return;
        }
        _accountId = account.Id;
        _memberId = member.Id;
        _today = DateOnly.FromDateTime(DateTime.Today);
        DateLabel = "Today";

        _memberData = await _repo.GetMemberDataAsync(account.Id, member.Id);
        _shared = await _repo.GetSharedLibraryAsync(account.Id);
        _manufacturer = await _repo.GetManufacturerLibraryAsync();
        Rebuild();
    }

    private string EntryName(LoggedFoodEntry entry)
    {
        if (entry.Label is not null) return entry.Label;
        if (entry.FoodItemId is Guid foodId)
        {
            return _shared.Foods.FirstOrDefault(f => f.Id == foodId)?.Name
                ?? _manufacturer.Foods.FirstOrDefault(f => f.Id == foodId)?.Name ?? "Food";
        }
        if (entry.RecipeId is Guid recipeId)
        {
            return _shared.Recipes.FirstOrDefault(r => r.Id == recipeId)?.Name ?? "Recipe";
        }
        return "Food";
    }

    private void Rebuild()
    {
        var todaysMeals = _memberData.Meals.Where(m => m.Date == _today).ToList();
        var goals = _memberData.MacroGoals;

        var totalCalories = todaysMeals.SelectMany(m => m.Entries).Sum(e => e.Calories);
        var totalProtein = todaysMeals.SelectMany(m => m.Entries).Sum(e => e.ProteinG);
        var totalCarbs = todaysMeals.SelectMany(m => m.Entries).Sum(e => e.CarbsG);
        var totalFat = todaysMeals.SelectMany(m => m.Entries).Sum(e => e.FatG);

        CaloriesLabel = $"{totalCalories:0} / {goals.Calories:0} kcal";
        CaloriesProgress = Ratio(totalCalories, goals.Calories);
        ProteinLabel = $"{totalProtein:0} / {goals.ProteinG:0} g protein";
        ProteinProgress = Ratio(totalProtein, goals.ProteinG);
        CarbsLabel = $"{totalCarbs:0} / {goals.CarbsG:0} g carbs";
        CarbsProgress = Ratio(totalCarbs, goals.CarbsG);
        FatLabel = $"{totalFat:0} / {goals.FatG:0} g fat";
        FatProgress = Ratio(totalFat, goals.FatG);

        // Replacing the whole collection (rather than Clear() + Add() in place) avoids
        // BindableLayout briefly seeing an empty source mid-rebuild — that transient
        // empty state crashes natively inside WinUI's own child-collection handling
        // (see the same fix in WorkoutsViewModel.Rebuild()).
        var groups = new ObservableCollection<MealGroupViewModel>();
        foreach (var mealType in Enum.GetValues<MealType>())
        {
            var meal = todaysMeals.FirstOrDefault(m => m.MealType == mealType);
            var entries = new ObservableCollection<LoggedEntryRowViewModel>();
            foreach (var entry in meal?.Entries ?? new List<LoggedFoodEntry>())
            {
                var name = EntryName(entry);
                var quantityLabel = entry.Quantity == 1 ? "1 serving" : $"{entry.Quantity:0.##} servings";
                var macrosLabel = $"{entry.Calories:0} kcal · {entry.ProteinG:0}p / {entry.CarbsG:0}c / {entry.FatG:0}f";
                entries.Add(new LoggedEntryRowViewModel(name, quantityLabel, macrosLabel, entry, RemoveEntryCommand));
            }

            var mealCalories = entries.Count > 0 ? meal!.Entries.Sum(e => e.Calories) : 0;
            var totalsLabel = entries.Count > 0 ? $"{mealCalories:0} kcal" : "Nothing logged";
            groups.Add(new MealGroupViewModel(mealType, mealType.ToString(), entries, totalsLabel, AddFoodCommand));
        }
        MealGroups = groups;
    }

    private static double Ratio(double value, double goal) => goal > 0 ? Math.Clamp(value / goal, 0, 1) : 0;

    [RelayCommand]
    private async Task AddFood(MealType mealType) =>
        await Shell.Current.GoToAsync($"addFoodEntry?mealType={mealType}");

    [RelayCommand]
    private async Task RemoveEntry(LoggedEntryRowViewModel row)
    {
        var meal = _memberData.Meals.FirstOrDefault(m => m.Entries.Contains(row.Entry));
        if (meal is null) return;
        meal.Entries.Remove(row.Entry);
        meal.UpdatedAt = DateTimeOffset.UtcNow;
        await _repo.SaveMemberDataAsync(_accountId, _memberId, _memberData);
        Rebuild();
    }

    [RelayCommand]
    private async Task OpenFoodLibrary() => await Shell.Current.GoToAsync("foodLibrary");

    [RelayCommand]
    private async Task OpenRecipes() => await Shell.Current.GoToAsync("recipeLibrary");

    [RelayCommand]
    private async Task OpenMacroGoals() => await Shell.Current.GoToAsync("macroGoals");
}

public class MealGroupViewModel
{
    public MealType MealType { get; }
    public string Title { get; }
    public ObservableCollection<LoggedEntryRowViewModel> Entries { get; }
    public string TotalsLabel { get; }
    public bool IsEmpty => Entries.Count == 0;
    public IRelayCommand<MealType> AddCommand { get; }

    public MealGroupViewModel(MealType mealType, string title, ObservableCollection<LoggedEntryRowViewModel> entries, string totalsLabel, IRelayCommand<MealType> addCommand)
    {
        MealType = mealType;
        Title = title;
        Entries = entries;
        TotalsLabel = totalsLabel;
        AddCommand = addCommand;
    }
}

public class LoggedEntryRowViewModel
{
    public string Name { get; }
    public string QuantityLabel { get; }
    public string MacrosLabel { get; }
    public LoggedFoodEntry Entry { get; }
    public IRelayCommand<LoggedEntryRowViewModel> RemoveCommand { get; }

    public LoggedEntryRowViewModel(string name, string quantityLabel, string macrosLabel, LoggedFoodEntry entry, IRelayCommand<LoggedEntryRowViewModel> removeCommand)
    {
        Name = name;
        QuantityLabel = quantityLabel;
        MacrosLabel = macrosLabel;
        Entry = entry;
        RemoveCommand = removeCommand;
    }
}
