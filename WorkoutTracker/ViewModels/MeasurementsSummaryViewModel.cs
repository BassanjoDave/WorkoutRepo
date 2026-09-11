using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Controls;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.ViewModels;

/// <summary>Home dashboard card for the Measurements module — latest weigh-in plus a mini trend over a chosen range, tap-through to the full page. HomePage.xaml.cs decides whether this view or a locked-state placeholder gets shown at all — this ViewModel only ever loads when access is confirmed.</summary>
public partial class MeasurementsSummaryViewModel : ObservableObject
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;

    private enum RangeOption { Week, Month, Year, All }
    private List<BodyMeasurementEntry> _measurements = new();
    private string _weightUnit = "lbs";
    private RangeOption _selectedRange = RangeOption.Month;

    [ObservableProperty] public partial string LatestLabel { get; set; } = "No weigh-ins logged yet";
    [ObservableProperty] public partial WeightTrendDrawable TrendDrawable { get; set; } = new();
    [ObservableProperty] public partial ObservableCollection<MetricChipViewModel> RangeChips { get; set; } = new();

    public MeasurementsSummaryViewModel(IActiveSessionService session, IWorkoutRepository repo)
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
        _measurements = memberData.Measurements;
        _weightUnit = memberData.WeightUnit;

        if (RangeChips.Count == 0)
        {
            foreach (var r in Enum.GetNames<RangeOption>()) RangeChips.Add(new MetricChipViewModel(r, r == _selectedRange.ToString(), SelectRangeCommand));
        }

        RebuildTrend();
    }

    [RelayCommand]
    private void SelectRange(string range)
    {
        _selectedRange = Enum.Parse<RangeOption>(range);
        foreach (var chip in RangeChips) chip.IsSelected = chip.Name == range;
        RebuildTrend();
    }

    private void RebuildTrend()
    {
        var allPoints = _measurements
            .Where(m => m.Weight.HasValue)
            .OrderBy(m => m.Date)
            .Select(m => (m.Date, Value: m.Weight!.Value))
            .ToList();
        if (allPoints.Count > 0)
        {
            var latest = allPoints[^1];
            LatestLabel = $"{latest.Value:0.#} {_weightUnit} on {latest.Date.ToDateTime(TimeOnly.MinValue):MMM d}";
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var from = _selectedRange switch
        {
            RangeOption.Week => today.AddDays(-7),
            RangeOption.Month => today.AddMonths(-1),
            RangeOption.Year => today.AddYears(-1),
            _ => DateOnly.MinValue,
        };

        TrendDrawable = new WeightTrendDrawable
        {
            Points = allPoints.Where(p => p.Date >= from).ToList(),
            LineColor = AppColors.Get("ColorAccent"),
        };
    }

    [RelayCommand]
    private async Task Open() => await Shell.Current.GoToAsync("//measurements");
}
