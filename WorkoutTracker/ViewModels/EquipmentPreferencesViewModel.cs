using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// Lets a member choose which equipment/manufacturer types the Library shows,
/// grouped as a folder tree (main category → individual items), with a
/// per-category "select all" and a way to add custom equipment/manufacturer
/// types beyond the built-in set.
/// </summary>
public partial class EquipmentPreferencesViewModel : ObservableObject
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;

    private Guid _accountId;
    private Guid _memberId;
    private SharedLibrary _shared = new();
    private MemberData _memberData = new();

    [ObservableProperty] public partial ObservableCollection<EquipmentGroupViewModel> Groups { get; set; } = new();

    public EquipmentPreferencesViewModel(IActiveSessionService session, IWorkoutRepository repo)
    {
        _session = session;
        _repo = repo;
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
        _accountId = account.Id;
        _memberId = member.Id;

        _shared = await _repo.GetSharedLibraryAsync(account.Id);
        _memberData = await _repo.GetMemberDataAsync(account.Id, member.Id);
        var enabled = _memberData.LibraryEquipmentFilter;

        // Replacing the whole collection (rather than Clear() + Add() in place) avoids
        // BindableLayout briefly seeing an empty source mid-rebuild — that transient
        // empty state crashes natively inside WinUI's own child-collection handling
        // (see the same fix in WorkoutsViewModel.Rebuild()).
        var groups = new ObservableCollection<EquipmentGroupViewModel>();
        foreach (var mainCategory in Enum.GetValues<EquipmentMainCategory>())
        {
            var items = new List<EquipmentItemViewModel>();
            foreach (var eq in Enum.GetValues<ExerciseEquipment>())
            {
                if (eq == ExerciseEquipment.Custom) continue; // "Custom" is the escape hatch, not a real filterable type
                if (EquipmentCatalog.MainCategoryOf(eq) != mainCategory) continue;
                var key = EquipmentCatalog.KeyFor(eq);
                items.Add(new EquipmentItemViewModel(key, EquipmentCatalog.Label(eq), enabled is null || enabled.Contains(key)));
            }
            foreach (var custom in _shared.CustomEquipmentTypes.Where(c => c.MainCategory == mainCategory))
            {
                var key = EquipmentCatalog.KeyFor(custom);
                items.Add(new EquipmentItemViewModel(key, custom.Name, enabled is null || enabled.Contains(key)));
            }
            if (items.Count == 0) continue;
            groups.Add(new EquipmentGroupViewModel(mainCategory, EquipmentCatalog.MainCategoryLabel(mainCategory), items));
        }
        Groups = groups;
    }

    [RelayCommand]
    private async Task AddCustomType()
    {
        var page = Shell.Current?.CurrentPage;
        if (page is null) return;

        var name = await page.DisplayPromptAsync("Add equipment type", "Name (e.g. a manufacturer or machine)", "Add", "Cancel");
        if (string.IsNullOrWhiteSpace(name)) return;

        var categoryLabels = Enum.GetValues<EquipmentMainCategory>().Select(EquipmentCatalog.MainCategoryLabel).ToArray();
        var categoryChoice = await page.DisplayActionSheetAsync("Which category?", "Cancel", null, categoryLabels);
        if (categoryChoice is null || categoryChoice == "Cancel") return;
        var mainCategory = Enum.GetValues<EquipmentMainCategory>()
            .FirstOrDefault(c => EquipmentCatalog.MainCategoryLabel(c) == categoryChoice);

        var custom = new CustomEquipmentType { Id = Guid.NewGuid(), Name = name.Trim(), MainCategory = mainCategory };
        _shared.CustomEquipmentTypes.Add(custom);
        _shared.UpdatedAt = DateTimeOffset.UtcNow;
        await _repo.SaveSharedLibraryAsync(_accountId, _shared);

        var group = Groups.FirstOrDefault(g => g.MainCategory == mainCategory);
        var key = EquipmentCatalog.KeyFor(custom);
        if (group is null)
        {
            group = new EquipmentGroupViewModel(mainCategory, EquipmentCatalog.MainCategoryLabel(mainCategory), new List<EquipmentItemViewModel>());
            Groups.Add(group);
        }
        group.Items.Add(new EquipmentItemViewModel(key, custom.Name, true));
    }

    [RelayCommand]
    private async Task Save()
    {
        _memberData.LibraryEquipmentFilter = Groups.SelectMany(g => g.Items).Where(i => i.IsSelected).Select(i => i.Key).ToList();
        await _repo.SaveMemberDataAsync(_accountId, _memberId, _memberData);
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task Cancel() => await Shell.Current.GoToAsync("..");
}

public partial class EquipmentGroupViewModel : ObservableObject
{
    public EquipmentMainCategory MainCategory { get; }
    public string Label { get; }
    public ObservableCollection<EquipmentItemViewModel> Items { get; }

    [ObservableProperty] public partial bool AllSelected { get; set; }

    private bool _suppressCascade;

    public EquipmentGroupViewModel(EquipmentMainCategory mainCategory, string label, List<EquipmentItemViewModel> items)
    {
        MainCategory = mainCategory;
        Label = label;
        Items = new ObservableCollection<EquipmentItemViewModel>(items);
        foreach (var item in Items) item.PropertyChanged += (_, _) => RecomputeAllSelected();
        RecomputeAllSelected();
    }

    partial void OnAllSelectedChanged(bool value)
    {
        if (_suppressCascade) return;
        _suppressCascade = true;
        foreach (var item in Items) item.IsSelected = value;
        _suppressCascade = false;
    }

    private void RecomputeAllSelected()
    {
        if (_suppressCascade) return;
        _suppressCascade = true;
        AllSelected = Items.Count > 0 && Items.All(i => i.IsSelected);
        _suppressCascade = false;
    }
}

public partial class EquipmentItemViewModel : ObservableObject
{
    public string Key { get; }
    public string Label { get; }

    [ObservableProperty] public partial bool IsSelected { get; set; }

    public EquipmentItemViewModel(string key, string label, bool isSelected)
    {
        Key = key;
        Label = label;
        IsSelected = isSelected;
    }
}
