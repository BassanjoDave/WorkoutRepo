using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.ViewModels;

public partial class MacroGoalsViewModel : ObservableObject
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;

    private Guid _accountId;
    private Guid _memberId;
    private MemberData _memberData = new();

    [ObservableProperty] public partial string Calories { get; set; } = "";
    [ObservableProperty] public partial string Protein { get; set; } = "";
    [ObservableProperty] public partial string Carbs { get; set; } = "";
    [ObservableProperty] public partial string Fat { get; set; } = "";

    [ObservableProperty] public partial bool ShowCaloriesOnHome { get; set; }
    [ObservableProperty] public partial bool ShowProteinOnHome { get; set; }
    [ObservableProperty] public partial bool ShowCarbsOnHome { get; set; }
    [ObservableProperty] public partial bool ShowFatOnHome { get; set; }

    public MacroGoalsViewModel(IActiveSessionService session, IWorkoutRepository repo)
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
        Calories = _memberData.MacroGoals.Calories.ToString("0");
        Protein = _memberData.MacroGoals.ProteinG.ToString("0");
        Carbs = _memberData.MacroGoals.CarbsG.ToString("0");
        Fat = _memberData.MacroGoals.FatG.ToString("0");
        ShowCaloriesOnHome = _memberData.MacroGoals.ShowCaloriesOnHome;
        ShowProteinOnHome = _memberData.MacroGoals.ShowProteinOnHome;
        ShowCarbsOnHome = _memberData.MacroGoals.ShowCarbsOnHome;
        ShowFatOnHome = _memberData.MacroGoals.ShowFatOnHome;
    }

    [RelayCommand]
    private async Task Save()
    {
        _memberData.MacroGoals = new MacroGoals
        {
            Calories = ParseDouble(Calories, 2000),
            ProteinG = ParseDouble(Protein, 150),
            CarbsG = ParseDouble(Carbs, 200),
            FatG = ParseDouble(Fat, 65),
            ShowCaloriesOnHome = ShowCaloriesOnHome,
            ShowProteinOnHome = ShowProteinOnHome,
            ShowCarbsOnHome = ShowCarbsOnHome,
            ShowFatOnHome = ShowFatOnHome,
        };
        await _repo.SaveMemberDataAsync(_accountId, _memberId, _memberData);
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task Cancel() => await Shell.Current.GoToAsync("..");

    private static double ParseDouble(string value, double fallback) => double.TryParse(value, out var d) ? d : fallback;
}
