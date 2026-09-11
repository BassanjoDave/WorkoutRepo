using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// The system-wide Rig catalog, minus whatever the member has already added
/// to My Rigs — pushed from MyRigsPage's "+ Add a Rig" button, pops back on
/// selection. See MyRigsViewModel for the primary "your Rigs" screen.
/// </summary>
public partial class BrowseRigsViewModel : ObservableObject
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;

    private Guid _accountId;
    private Guid _memberId;
    private MemberData _memberData = new();
    private RigCatalog _catalog = new();

    [ObservableProperty] public partial ObservableCollection<BrowseRigRowViewModel> Rigs { get; set; } = new();

    public BrowseRigsViewModel(IActiveSessionService session, IWorkoutRepository repo)
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

        _catalog = await _repo.GetRigCatalogAsync();
        _memberData = await _repo.GetMemberDataAsync(account.Id, member.Id);

        var rigs = new ObservableCollection<BrowseRigRowViewModel>();
        foreach (var rig in _catalog.Rigs.Where(r => !_memberData.MyRigIds.Contains(r.Id)))
        {
            rigs.Add(new BrowseRigRowViewModel(rig.Id, rig.Name, rig.Manufacturer, rig.Description,
                addCommand: new AsyncRelayCommand(() => AddRigAsync(rig.Id, rig.EquipmentKey))));
        }
        Rigs = rigs;
    }

    private async Task AddRigAsync(Guid rigId, string equipmentKey)
    {
        _memberData.MyRigIds.Add(rigId);
        // Only unlock the key when the filter has been customized — null already
        // means "show everything," so touching it there would be a no-op anyway.
        _memberData.LibraryEquipmentFilter?.Add(equipmentKey);
        _memberData.UpdatedAt = DateTimeOffset.UtcNow;
        await _repo.SaveMemberDataAsync(_accountId, _memberId, _memberData);
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task Back() => await Shell.Current.GoToAsync("..");
}

public class BrowseRigRowViewModel
{
    public Guid Id { get; }
    public string Name { get; }
    public string Manufacturer { get; }
    public string? Description { get; }
    public IAsyncRelayCommand AddCommand { get; }

    public BrowseRigRowViewModel(Guid id, string name, string manufacturer, string? description, IAsyncRelayCommand addCommand)
    {
        Id = id;
        Name = name;
        Manufacturer = manufacturer;
        Description = description;
        AddCommand = addCommand;
    }
}
