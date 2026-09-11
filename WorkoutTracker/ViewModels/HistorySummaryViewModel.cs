using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.ViewModels;

/// <summary>Home dashboard card for the History module — current streak and total logged workouts, tap-through to the full History tab.</summary>
public partial class HistorySummaryViewModel : ObservableObject
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;

    [ObservableProperty] public partial int StreakCount { get; set; }
    [ObservableProperty] public partial int HistoryCount { get; set; }

    public HistorySummaryViewModel(IActiveSessionService session, IWorkoutRepository repo)
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
        var completedDates = memberData.Sessions.Where(s => s.Status == SessionStatus.Completed).Select(s => s.Date).ToHashSet();
        HistoryCount = completedDates.Count;
        StreakCount = ComputeStreak(completedDates);
    }

    /// <summary>Consecutive completed days ending today; counts back from yesterday if today isn't logged yet — same rule as before this became its own module.</summary>
    private static int ComputeStreak(HashSet<DateOnly> completedDates)
    {
        var day = DateOnly.FromDateTime(DateTime.Today);
        if (!completedDates.Contains(day)) day = day.AddDays(-1);

        var streak = 0;
        while (completedDates.Contains(day))
        {
            streak++;
            day = day.AddDays(-1);
        }
        return streak;
    }

    [RelayCommand]
    private async Task Open() => await Shell.Current.GoToAsync("//history");
}
