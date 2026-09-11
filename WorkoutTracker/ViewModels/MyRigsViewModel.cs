using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// A member's own collection of commercial machines (Rigs) they've added from
/// the system-wide catalog — see Rig/RigCatalog. Each card shows how many of
/// the Library's exercises/routines are tailored to that Rig's equipment key
/// (EquipmentCatalog.KeyFor), plus optional PDF/affiliate links that simply
/// don't render when the Rig has no URL on file.
/// </summary>
public partial class MyRigsViewModel : ObservableObject
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;

    private Guid _accountId;
    private Guid _memberId;
    private MemberData _memberData = new();
    private RigCatalog _catalog = new();
    private List<Exercise> _allExercises = new();
    private List<RoutineDefinition> _allRoutines = new();

    [ObservableProperty] public partial ObservableCollection<RigCardViewModel> Rigs { get; set; } = new();

    public MyRigsViewModel(IActiveSessionService session, IWorkoutRepository repo)
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

        var shared = await _repo.GetSharedLibraryAsync(account.Id);
        var manufacturer = await _repo.GetManufacturerLibraryAsync();
        _catalog = await _repo.GetRigCatalogAsync();
        _memberData = await _repo.GetMemberDataAsync(account.Id, member.Id);
        _allExercises = shared.Exercises.Concat(manufacturer.Exercises).ToList();
        _allRoutines = shared.Routines.Concat(manufacturer.Routines).ToList();

        Rebuild();
    }

    // Replacing the whole collection (rather than Clear() + Add() in place) avoids
    // BindableLayout briefly seeing an empty source mid-rebuild — see the same fix
    // in WorkoutsViewModel.Rebuild().
    private void Rebuild()
    {
        var rigs = new ObservableCollection<RigCardViewModel>();
        foreach (var rigId in _memberData.MyRigIds)
        {
            var rig = _catalog.Rigs.FirstOrDefault(r => r.Id == rigId);
            if (rig is null) continue; // seed data can shrink/rename between versions

            var exerciseIds = _allExercises
                .Where(e => EquipmentCatalog.KeyFor(e) == rig.EquipmentKey)
                .Select(e => e.Id)
                .ToHashSet();
            var routineCount = _allRoutines.Count(r => r.Exercises.Any(t => exerciseIds.Contains(t.ExerciseId)));

            rigs.Add(new RigCardViewModel(
                rig.Id, rig.Name, rig.Manufacturer, rig.Description,
                exerciseIds.Count, routineCount, rig.PdfUrl, rig.AffiliateUrl,
                viewPdfCommand: new AsyncRelayCommand(() => OpenUrlAsync(rig.PdfUrl)),
                shopCommand: new AsyncRelayCommand(() => OpenUrlAsync(rig.AffiliateUrl)),
                removeCommand: new AsyncRelayCommand(() => RemoveRigAsync(rig.Id)),
                viewInLibraryCommand: new AsyncRelayCommand(() => ViewInLibraryAsync(rig.EquipmentKey))));
        }
        Rigs = rigs;
    }

    private static async Task OpenUrlAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        await Microsoft.Maui.ApplicationModel.Launcher.Default.OpenAsync(url);
    }

    private async Task RemoveRigAsync(Guid rigId)
    {
        _memberData.MyRigIds.Remove(rigId);
        _memberData.UpdatedAt = DateTimeOffset.UtcNow;
        await _repo.SaveMemberDataAsync(_accountId, _memberId, _memberData);
        Rebuild();
    }

    [RelayCommand]
    private async Task AddRig() => await Shell.Current.GoToAsync("browseRigs");

    [RelayCommand]
    private async Task GoToLibrary() => await Shell.Current.GoToAsync("//exercise");

    /// <summary>Deep-links into the Library tab with this Rig's equipment filter
    /// pre-selected — see LibraryViewModel.PreSelectEquipment.</summary>
    private static async Task ViewInLibraryAsync(string equipmentKey) =>
        await Shell.Current.GoToAsync($"//exercise?equipmentKey={Uri.EscapeDataString(equipmentKey)}");

    [RelayCommand]
    private async Task Back() => await Shell.Current.GoToAsync("..");
}

public class RigCardViewModel
{
    public Guid Id { get; }
    public string Name { get; }
    public string Manufacturer { get; }
    public string? Description { get; }
    public int ExerciseCount { get; }
    public int RoutineCount { get; }
    public bool HasPdf { get; }
    public bool HasAffiliate { get; }

    public IAsyncRelayCommand ViewPdfCommand { get; }
    public IAsyncRelayCommand ShopCommand { get; }
    public IAsyncRelayCommand RemoveCommand { get; }
    public IAsyncRelayCommand ViewInLibraryCommand { get; }

    public RigCardViewModel(Guid id, string name, string manufacturer, string? description,
        int exerciseCount, int routineCount, string? pdfUrl, string? affiliateUrl,
        IAsyncRelayCommand viewPdfCommand, IAsyncRelayCommand shopCommand, IAsyncRelayCommand removeCommand,
        IAsyncRelayCommand viewInLibraryCommand)
    {
        Id = id;
        Name = name;
        Manufacturer = manufacturer;
        Description = description;
        ExerciseCount = exerciseCount;
        RoutineCount = routineCount;
        HasPdf = !string.IsNullOrWhiteSpace(pdfUrl);
        HasAffiliate = !string.IsNullOrWhiteSpace(affiliateUrl);
        ViewPdfCommand = viewPdfCommand;
        ShopCommand = shopCommand;
        RemoveCommand = removeCommand;
        ViewInLibraryCommand = viewInLibraryCommand;
    }
}
