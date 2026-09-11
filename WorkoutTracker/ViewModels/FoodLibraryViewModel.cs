using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;
using Visibility = WorkoutTracker.Models.Visibility;

namespace WorkoutTracker.ViewModels;

public partial class FoodLibraryViewModel : ObservableObject
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;

    private Guid _activeMemberId;
    private List<FoodItem> _allFoods = new();

    [ObservableProperty] public partial string SearchText { get; set; } = "";
    [ObservableProperty] public partial ObservableCollection<FoodRowViewModel> Foods { get; set; } = new();

    public FoodLibraryViewModel(IActiveSessionService session, IWorkoutRepository repo)
    {
        _session = session;
        _repo = repo;
    }

    partial void OnSearchTextChanged(string value) => Rebuild();

    public async Task LoadAsync()
    {
        var account = _session.ActiveAccount;
        var member = _session.ActiveMember;
        if (account is null || member is null)
        {
            await Shell.Current.GoToAsync("//gate");
            return;
        }
        _activeMemberId = member.Id;

        var shared = await _repo.GetSharedLibraryAsync(account.Id);
        var manufacturer = await _repo.GetManufacturerLibraryAsync();
        _allFoods = shared.Foods.Concat(manufacturer.Foods).OrderBy(f => f.Name).ToList();

        Rebuild();
    }

    private void Rebuild()
    {
        var needle = SearchText.Trim();

        // Replacing the whole collection (rather than Clear() + Add() in place) avoids
        // BindableLayout briefly seeing an empty source mid-rebuild — that transient
        // empty state crashes natively inside WinUI's own child-collection handling
        // (see the same fix in WorkoutsViewModel.Rebuild()).
        var foods = new ObservableCollection<FoodRowViewModel>();
        foreach (var f in _allFoods.Where(f => string.IsNullOrEmpty(needle)
                     || f.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)
                     || (f.BrandName?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false)))
        {
            var badge = f.Visibility == Visibility.Manufacturer ? null : f.OwnerMemberId == _activeMemberId ? null : "Account";
            var namePart = string.IsNullOrWhiteSpace(f.BrandName) ? f.Name : $"{f.BrandName} {f.Name}";
            foods.Add(new FoodRowViewModel(namePart, $"{f.ServingLabel} · {f.Calories:0} kcal · {f.ProteinG:0}p / {f.CarbsG:0}c / {f.FatG:0}f", badge));
        }
        Foods = foods;
    }

    [RelayCommand]
    private async Task Back() => await Shell.Current.GoToAsync("..");

    [RelayCommand]
    private async Task AddFood() => await Shell.Current.GoToAsync("foodEditor");

    [RelayCommand]
    private async Task OpenRecipes() => await Shell.Current.GoToAsync("recipeLibrary");
}

public class FoodRowViewModel
{
    public string Name { get; }
    public string SubtitleLabel { get; }
    public string? OwnerBadge { get; }

    public FoodRowViewModel(string name, string subtitleLabel, string? ownerBadge)
    {
        Name = name;
        SubtitleLabel = subtitleLabel;
        OwnerBadge = ownerBadge;
    }
}
