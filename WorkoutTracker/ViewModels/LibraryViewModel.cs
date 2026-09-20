using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;
using Visibility = WorkoutTracker.Models.Visibility;

namespace WorkoutTracker.ViewModels;

public partial class LibraryViewModel : ObservableObject
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;

    private List<Exercise> _allExercises = new();
    private List<string>? _libraryFilter;
    private List<Rig> _rigs = new();
    private Guid _activeMemberId;
    private string _selectedCategory = "All";
    private string _selectedEquipment = "All";

    [ObservableProperty] public partial bool ShowMine { get; set; } = true;
    [ObservableProperty] public partial bool ShowAccount { get; set; } = true;
    [ObservableProperty] public partial bool ShowManufacturer { get; set; } = true;
    [ObservableProperty] public partial string SearchText { get; set; } = "";
    [ObservableProperty] public partial ObservableCollection<ChipOptionViewModel> CategoryChips { get; set; } = new();
    [ObservableProperty] public partial ObservableCollection<ChipOptionViewModel> EquipmentChips { get; set; } = new();
    [ObservableProperty] public partial ObservableCollection<ExerciseGroupViewModel> Exercises { get; set; } = new();

    private static readonly string[] Categories = { "All", "Chest", "Shoulders", "Back", "Arms", "Abs", "Legs", "Full Body", "Cardio", "Vibration Plate" };
    private static readonly string[] EquipmentTypes =
    {
        "All", "Bowflex Rig", "Bodyweight", "Kettlebell", "Dumbbell", "Barbell", "Weight Bench",
        "Resistance Band", "Vibration Plate", "Stretch", "Cardio Rig", "Custom",
    };

    public LibraryViewModel(IActiveSessionService session, IWorkoutRepository repo)
    {
        _session = session;
        _repo = repo;
    }

    partial void OnShowMineChanged(bool value) => Rebuild();
    partial void OnShowAccountChanged(bool value) => Rebuild();
    partial void OnShowManufacturerChanged(bool value) => Rebuild();
    partial void OnSearchTextChanged(string value) => Rebuild();

    [RelayCommand] private void ToggleMine() => ShowMine = !ShowMine;
    [RelayCommand] private void ToggleAccount() => ShowAccount = !ShowAccount;
    [RelayCommand] private void ToggleManufacturer() => ShowManufacturer = !ShowManufacturer;

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
        var memberData = await _repo.GetMemberDataAsync(account.Id, member.Id);
        _allExercises = shared.Exercises.Concat(manufacturer.Exercises).ToList();
        _libraryFilter = memberData.LibraryEquipmentFilter;
        _rigs = (await _repo.GetRigCatalogAsync()).Rigs;

        CategoryChips = BuildChip(Categories, _selectedCategory, key => SelectCategoryCommand.Execute(key));
        EquipmentChips = BuildChip(EquipmentTypes, _selectedEquipment, key => SelectEquipmentCommand.Execute(key));

        Rebuild();
    }

    [RelayCommand]
    private async Task OpenEquipmentPreferences() => await Shell.Current.GoToAsync("equipmentPreferences");

    // Returns a fresh collection (rather than mutating one in place with Clear() + Add())
    // so callers can replace the whole ObservableProperty in one atomic step — see the
    // same fix in WorkoutsViewModel.Rebuild() for why Clear() is unsafe here: it briefly
    // leaves BindableLayout looking at an empty source, which crashes natively inside
    // WinUI's own child-collection handling.
    private static ObservableCollection<ChipOptionViewModel> BuildChip(string[] options, string selected, Action<string> select)
    {
        var chips = new ObservableCollection<ChipOptionViewModel>();
        foreach (var opt in options)
        {
            chips.Add(new ChipOptionViewModel(opt, opt == selected, new RelayCommand(() => select(opt))));
        }
        return chips;
    }

    [RelayCommand]
    private void SelectCategory(string key)
    {
        _selectedCategory = key;
        CategoryChips = BuildChip(Categories, _selectedCategory, k => SelectCategoryCommand.Execute(k));
        Rebuild();
    }

    [RelayCommand]
    private void SelectEquipment(string key)
    {
        _selectedEquipment = key;
        EquipmentChips = BuildChip(EquipmentTypes, _selectedEquipment, k => SelectEquipmentCommand.Execute(k));
        Rebuild();
    }

    /// <summary>Deep-link entry point from a My Rigs card — equipmentKey is the
    /// EquipmentCatalog.KeyFor() format (an ExerciseEquipment enum name), resolved
    /// here to whatever label the existing filter chips use. Only enum-backed keys
    /// are supported (custom equipment types share one "Custom" chip today with no
    /// per-type filter, so there's nothing more specific to select for those).
    /// Must be called after LoadAsync, since it just re-selects an existing chip.</summary>
    public void PreSelectEquipment(string equipmentKey)
    {
        foreach (var eq in Enum.GetValues<ExerciseEquipment>())
        {
            if (EquipmentCatalog.KeyFor(eq) != equipmentKey) continue;
            SelectEquipmentCommand.Execute(EquipmentCatalog.Label(eq));
            return;
        }
    }

    private bool VisibilityOk(Exercise e)
    {
        if (e.Visibility == Visibility.Manufacturer) return ShowManufacturer;
        if (e.OwnerMemberId == _activeMemberId) return ShowMine;
        if (e.Visibility == Visibility.Account) return ShowAccount;
        return false;
    }

    private void Rebuild()
    {
        // Replacing the whole collection (rather than Clear() + Add() in place) avoids
        // BindableLayout briefly seeing an empty source mid-rebuild — that transient
        // empty state crashes natively inside WinUI's own child-collection handling
        // (see the same fix in WorkoutsViewModel.Rebuild()).
        var needle = SearchText.Trim();
        var filtered = _allExercises
            .Where(VisibilityOk)
            .Where(e => EquipmentCatalog.IsExerciseEnabled(_libraryFilter, e))
            .Where(e => _selectedCategory == "All" || CategoryLabel(e.Category) == _selectedCategory)
            .Where(e => _selectedEquipment == "All" || EquipmentLabel(e.Equipment) == _selectedEquipment)
            .Where(e => string.IsNullOrEmpty(needle) || e.Name.Contains(needle, StringComparison.OrdinalIgnoreCase));

        // The same exercise name can exist on several machines/equipment types (e.g.
        // "Biceps Curl" on a Bowflex vs. as a Dumbbell exercise) — group by name so the
        // list shows one row per distinct exercise, with a chip per machine/type variant
        // rather than a separate full row for every variant.
        var groups = filtered
            .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key);

        var exercises = new ObservableCollection<ExerciseGroupViewModel>();
        foreach (var group in groups)
        {
            var variants = group
                .OrderBy(e => EquipmentCatalog.MachineLabel(e, _rigs))
                .Select(e => new ExerciseRowViewModel(e.Id, e.Name, e.Muscles, EquipmentCatalog.MachineLabel(e, _rigs)))
                .ToList();
            exercises.Add(new ExerciseGroupViewModel(group.Key, group.First().Muscles, variants));
        }
        Exercises = exercises;
    }

    private static string CategoryLabel(ExerciseCategory c) => c switch
    {
        ExerciseCategory.FullBody => "Full Body",
        ExerciseCategory.VibrationPlate => "Vibration Plate",
        _ => c.ToString(),
    };

    private static string EquipmentLabel(ExerciseEquipment eq) => EquipmentCatalog.Label(eq);
}

public class ChipOptionViewModel
{
    public string Label { get; }
    public bool IsActive { get; }
    public ICommand SelectCommand { get; }

    public ChipOptionViewModel(string label, bool isActive, ICommand selectCommand)
    {
        Label = label;
        IsActive = isActive;
        SelectCommand = selectCommand;
    }
}

/// <summary>One row in the grouped Exercise Library — a distinct exercise name,
/// shown once regardless of how many machines/equipment types it's available on.</summary>
public class ExerciseGroupViewModel
{
    public string Name { get; }
    public string Muscles { get; }
    public List<ExerciseRowViewModel> Variants { get; }

    public ExerciseGroupViewModel(string name, string muscles, List<ExerciseRowViewModel> variants)
    {
        Name = name;
        Muscles = muscles;
        Variants = variants;
    }
}

/// <summary>One specific machine/equipment variant of an ExerciseGroupViewModel — a real
/// Exercise record, rendered as a tappable chip labeled with its machine/type.</summary>
public class ExerciseRowViewModel
{
    public Guid Id { get; }
    public string Name { get; }
    public string Muscles { get; }
    public string VariantLabel { get; }

    public ExerciseRowViewModel(Guid id, string name, string muscles, string variantLabel)
    {
        Id = id;
        Name = name;
        Muscles = muscles;
        VariantLabel = variantLabel;
    }
}
