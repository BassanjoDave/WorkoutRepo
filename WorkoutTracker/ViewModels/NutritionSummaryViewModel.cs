using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.ViewModels;

/// <summary>Home dashboard card for the Nutrition module — today's running totals for whichever macros the member opted into on the Macro Goals screen, tap-through to the full Nutrition tab. Shows nothing (Home hides the card) when none are opted in. HomePage.xaml.cs decides whether this view or a locked-state placeholder gets shown at all — this ViewModel only ever loads when access is confirmed.</summary>
public partial class NutritionSummaryViewModel : ObservableObject
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;

    [ObservableProperty] public partial bool ShowCalories { get; set; }
    [ObservableProperty] public partial string CaloriesLabel { get; set; } = "";
    [ObservableProperty] public partial double CaloriesProgress { get; set; }

    [ObservableProperty] public partial bool ShowProtein { get; set; }
    [ObservableProperty] public partial string ProteinLabel { get; set; } = "";
    [ObservableProperty] public partial double ProteinProgress { get; set; }

    [ObservableProperty] public partial bool ShowCarbs { get; set; }
    [ObservableProperty] public partial string CarbsLabel { get; set; } = "";
    [ObservableProperty] public partial double CarbsProgress { get; set; }

    [ObservableProperty] public partial bool ShowFat { get; set; }
    [ObservableProperty] public partial string FatLabel { get; set; } = "";
    [ObservableProperty] public partial double FatProgress { get; set; }

    [ObservableProperty] public partial bool ShowWater { get; set; }
    [ObservableProperty] public partial string WaterLabel { get; set; } = "";
    [ObservableProperty] public partial double WaterProgress { get; set; }

    [ObservableProperty] public partial bool HasAnySelected { get; set; }

    public NutritionSummaryViewModel(IActiveSessionService session, IWorkoutRepository repo)
    {
        _session = session;
        _repo = repo;
    }

    public async Task LoadAsync()
    {
        var account = _session.ActiveAccount;
        var member = _session.ActiveMember;
        if (account is null || member is null) return;

        var memberData = await _repo.GetMemberDataAsync(account.Id, member.Id);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var entries = memberData.Meals.Where(m => m.Date == today).SelectMany(m => m.Entries).ToList();
        var goals = memberData.MacroGoals;

        ShowCalories = goals.ShowCaloriesOnHome;
        (CaloriesLabel, CaloriesProgress) = Summarize(entries.Sum(e => e.Calories), goals.Calories, "kcal");

        ShowProtein = goals.ShowProteinOnHome;
        (ProteinLabel, ProteinProgress) = Summarize(entries.Sum(e => e.ProteinG), goals.ProteinG, "g protein");

        ShowCarbs = goals.ShowCarbsOnHome;
        (CarbsLabel, CarbsProgress) = Summarize(entries.Sum(e => e.CarbsG), goals.CarbsG, "g carbs");

        ShowFat = goals.ShowFatOnHome;
        (FatLabel, FatProgress) = Summarize(entries.Sum(e => e.FatG), goals.FatG, "g fat");

        ShowWater = goals.ShowWaterOnHome;
        var todaysWater = memberData.WaterLog.FirstOrDefault(w => w.Date == today)?.Ounces ?? 0;
        (WaterLabel, WaterProgress) = Summarize(todaysWater, goals.WaterOz, "oz water");

        HasAnySelected = ShowCalories || ShowProtein || ShowCarbs || ShowFat || ShowWater;
    }

    private static (string Label, double Progress) Summarize(double total, double goal, string unit) =>
        ($"{total:0} / {goal:0} {unit} today", goal > 0 ? Math.Clamp(total / goal, 0, 1) : 0);

    [RelayCommand]
    private async Task Open() => await Shell.Current.GoToAsync("//nutrition");
}
