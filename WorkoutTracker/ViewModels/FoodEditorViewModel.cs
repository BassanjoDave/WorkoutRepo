using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;
using Visibility = WorkoutTracker.Models.Visibility;

namespace WorkoutTracker.ViewModels;

/// <summary>Adds a custom food to the account's shared library — a member's own exact macro numbers for something not in the seed set.</summary>
public partial class FoodEditorViewModel : ObservableObject, IQueryAttributable
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;
    private readonly IRecipeDraftBridge _recipeDraftBridge;
    private bool _returnToRecipe;

    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial string BrandName { get; set; } = "";
    [ObservableProperty] public partial string ServingLabel { get; set; } = "";
    [ObservableProperty] public partial string Calories { get; set; } = "";
    [ObservableProperty] public partial string Protein { get; set; } = "";
    [ObservableProperty] public partial string Carbs { get; set; } = "";
    [ObservableProperty] public partial string Fat { get; set; } = "";
    [ObservableProperty] public partial string Sodium { get; set; } = "";
    [ObservableProperty] public partial string Fiber { get; set; } = "";
    [ObservableProperty] public partial string Sugar { get; set; } = "";
    [ObservableProperty] public partial string SugarAlcohol { get; set; } = "";
    [ObservableProperty] public partial string ErrorMessage { get; set; } = "";

    public FoodEditorViewModel(IActiveSessionService session, IWorkoutRepository repo, IRecipeDraftBridge recipeDraftBridge)
    {
        _session = session;
        _repo = repo;
        _recipeDraftBridge = recipeDraftBridge;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query) =>
        _returnToRecipe = query.TryGetValue("fromRecipe", out var v) && (string)v == "true";

    [RelayCommand]
    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Give it a name first.";
            return;
        }
        if (string.IsNullOrWhiteSpace(ServingLabel))
        {
            ErrorMessage = "Describe one serving (e.g. \"100 g\" or \"1 cup\").";
            return;
        }

        var account = _session.ActiveAccount;
        var member = _session.ActiveMember;
        if (account is null || member is null) return;

        var shared = await _repo.GetSharedLibraryAsync(account.Id);
        var newFood = new FoodItem
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            OwnerMemberId = member.Id,
            Name = Name.Trim(),
            BrandName = string.IsNullOrWhiteSpace(BrandName) ? null : BrandName.Trim(),
            ServingLabel = ServingLabel.Trim(),
            Calories = ParseDouble(Calories),
            ProteinG = ParseDouble(Protein),
            CarbsG = ParseDouble(Carbs),
            FatG = ParseDouble(Fat),
            SodiumMg = ParseDouble(Sodium),
            FiberG = ParseDouble(Fiber),
            SugarG = ParseDouble(Sugar),
            SugarAlcoholG = ParseDouble(SugarAlcohol),
            Visibility = Visibility.Account,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        shared.Foods.Add(newFood);
        await _repo.SaveSharedLibraryAsync(account.Id, shared);

        if (_returnToRecipe) _recipeDraftBridge.SetPendingFoodId(newFood.Id);
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task Cancel() => await Shell.Current.GoToAsync("..");

    private static double ParseDouble(string value) => double.TryParse(value, out var d) ? d : 0;
}
