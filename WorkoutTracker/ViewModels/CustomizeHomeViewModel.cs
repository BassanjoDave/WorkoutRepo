using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// Lets a member choose which Home dashboard modules are enabled and in what
/// order. Exercise is fixed at the top (it owns the day picker Home is built
/// around) — enable/disable only, no reordering; everything else can be
/// freely toggled and moved via up/down (no drag-and-drop, keeps this simple
/// and reliable across platforms).
/// </summary>
public partial class CustomizeHomeViewModel : ObservableObject
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;

    private Guid _accountId;
    private Guid _memberId;
    private MemberData _memberData = new();

    [ObservableProperty] public partial bool IsExerciseEnabled { get; set; } = true;
    [ObservableProperty] public partial ObservableCollection<CustomizeModuleRowViewModel> Modules { get; set; } = new();

    public CustomizeHomeViewModel(IActiveSessionService session, IWorkoutRepository repo)
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

        _memberData = await _repo.GetMemberDataAsync(account.Id, member.Id);
        var configs = _memberData.HomeModules;
        IsExerciseEnabled = configs.FirstOrDefault(m => m.ModuleId == "exercise")?.Enabled ?? true;

        List<string> orderedIds;
        Dictionary<string, bool> enabledMap;
        if (configs.Count == 0)
        {
            orderedIds = HomeModules.All.Select(m => m.Id).ToList();
            enabledMap = orderedIds.ToDictionary(id => id, _ => true);
        }
        else
        {
            orderedIds = configs.Where(m => m.ModuleId != "exercise").OrderBy(m => m.Order).Select(m => m.ModuleId).ToList();
            enabledMap = configs.Where(m => m.ModuleId != "exercise").ToDictionary(m => m.ModuleId, m => m.Enabled);
            // A module added in a later app update won't be in an existing member's saved list yet — tack it on enabled by default.
            foreach (var (id, _) in HomeModules.All)
            {
                if (!orderedIds.Contains(id)) orderedIds.Add(id);
            }
        }

        // Replacing the whole collection (rather than Clear() + Add() in place) avoids
        // BindableLayout briefly seeing an empty source mid-rebuild — that transient
        // empty state crashes natively inside WinUI's own child-collection handling
        // (see the same fix in WorkoutsViewModel.Rebuild()).
        var hideMedications = configs.FirstOrDefault(m => m.ModuleId == "stacks")?.SummaryVariant == "hideMeds";

        var modules = new ObservableCollection<CustomizeModuleRowViewModel>();
        foreach (var id in orderedIds)
        {
            var enabled = enabledMap.TryGetValue(id, out var e) ? e : true;
            modules.Add(new CustomizeModuleRowViewModel(id, HomeModules.TitleFor(id), enabled,
                id == "stacks" && hideMedications, MoveUpCommand, MoveDownCommand));
        }
        Modules = modules;
    }

    [RelayCommand]
    private void MoveUp(CustomizeModuleRowViewModel row)
    {
        var i = Modules.IndexOf(row);
        if (i > 0) Modules.Move(i, i - 1);
    }

    [RelayCommand]
    private void MoveDown(CustomizeModuleRowViewModel row)
    {
        var i = Modules.IndexOf(row);
        if (i >= 0 && i < Modules.Count - 1) Modules.Move(i, i + 1);
    }

    [RelayCommand]
    private async Task Save()
    {
        var configs = new List<HomeModuleConfig> { new() { ModuleId = "exercise", Enabled = IsExerciseEnabled, Order = 0 } };
        for (var i = 0; i < Modules.Count; i++)
        {
            var row = Modules[i];
            configs.Add(new HomeModuleConfig
            {
                ModuleId = row.ModuleId,
                Enabled = row.IsEnabled,
                Order = i + 1,
                SummaryVariant = row.ModuleId == "stacks" && row.HideMedications ? "hideMeds" : "default",
            });
        }
        _memberData.HomeModules = configs;
        await _repo.SaveMemberDataAsync(_accountId, _memberId, _memberData);
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task Cancel() => await Shell.Current.GoToAsync("..");
}

public partial class CustomizeModuleRowViewModel : ObservableObject
{
    public string ModuleId { get; }
    public string Title { get; }
    public bool IsStacksModule => ModuleId == "stacks";
    [ObservableProperty] public partial bool IsEnabled { get; set; }

    /// <summary>Only meaningful for the "stacks" module — see StacksSummaryViewModel,
    /// which reads this back via HomeModuleConfig.SummaryVariant == "hideMeds".</summary>
    [ObservableProperty] public partial bool HideMedications { get; set; }

    public IRelayCommand<CustomizeModuleRowViewModel> MoveUpCommand { get; }
    public IRelayCommand<CustomizeModuleRowViewModel> MoveDownCommand { get; }

    public CustomizeModuleRowViewModel(string moduleId, string title, bool isEnabled, bool hideMedications,
        IRelayCommand<CustomizeModuleRowViewModel> moveUp, IRelayCommand<CustomizeModuleRowViewModel> moveDown)
    {
        ModuleId = moduleId;
        Title = title;
        IsEnabled = isEnabled;
        HideMedications = hideMedications;
        MoveUpCommand = moveUp;
        MoveDownCommand = moveDown;
    }
}
