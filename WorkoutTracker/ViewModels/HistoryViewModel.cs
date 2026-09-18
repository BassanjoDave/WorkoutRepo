using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Controls;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.ViewModels;

public enum LogSortKey { Date, Exercise, Weight }
public enum HistoryTab { Overview, Progress, Log, Nutrition, Stacks, Measurement }

public partial class HistoryViewModel : ObservableObject
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;
    private readonly IEntitlementService _entitlements;

    private List<SetLogRow> _allLogRows = new();
    private List<WorkoutSession> _completedSessions = new();
    private string _weightUnit = "lbs";
    private Dictionary<string, List<(DateOnly Date, double Weight)>> _exerciseWeightHistory = new();
    private List<BodyMeasurementEntry> _measurements = new();
    private List<CustomMeasurementSlot> _customMeasurementSlots = new();

    private static readonly string[] BuiltInMeasurementMetrics =
        { "Weight", "Neck", "Shoulder", "Chest", "Waist", "Abdomen", "Hip", "L-Bicep", "R-Bicep", "L-Thigh", "R-Thigh", "L-Calf", "R-Calf" };
    // The month currently shown in the Overview calendar — starts on today's month,
    // moves independently once the user navigates via Previous/NextMonth.
    private DateOnly _calendarMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    [ObservableProperty] public partial HistoryTab SelectedTab { get; set; } = HistoryTab.Overview;
    public bool IsOverviewTab => SelectedTab == HistoryTab.Overview;
    public bool IsProgressTab => SelectedTab == HistoryTab.Progress;
    public bool IsLogTab => SelectedTab == HistoryTab.Log;
    public bool IsNutritionTab => SelectedTab == HistoryTab.Nutrition;
    public bool IsStacksTab => SelectedTab == HistoryTab.Stacks;
    public bool IsMeasurementTab => SelectedTab == HistoryTab.Measurement;

    // History stays visible read-only regardless of Full Access (see the comment on
    // ShowNutrition/ShowStacksHistory below) — these only drive the non-blocking
    // "your history stays visible" upgrade nudge on each tab, not whether it's reachable.
    [ObservableProperty] public partial bool HasNutritionAccess { get; set; }
    [ObservableProperty] public partial bool HasStacksAccess { get; set; }
    [ObservableProperty] public partial bool HasMeasurementsAccess { get; set; }

    [ObservableProperty] public partial ObservableCollection<MetricChipViewModel> MeasurementMetricChips { get; set; } = new();
    [ObservableProperty] public partial string SelectedMeasurementMetric { get; set; } = "Weight";
    [ObservableProperty] public partial WeightTrendDrawable MeasurementDrawable { get; set; } = new();
    [ObservableProperty] public partial string MeasurementBestLabel { get; set; } = "";
    [ObservableProperty] public partial bool HasMeasurementData { get; set; }

    [ObservableProperty] public partial ObservableCollection<NutritionDayRowViewModel> NutritionDayRows { get; set; } = new();
    [ObservableProperty] public partial bool NutritionEmpty { get; set; }

    [ObservableProperty] public partial ObservableCollection<NutritionDayRowViewModel> StackHistoryDayRows { get; set; } = new();
    [ObservableProperty] public partial bool StackHistoryEmpty { get; set; }

    /// <summary>Short (5-row) previews of the Nutrition/Stacks tabs' own full history,
    /// shown on Overview right alongside RecentSessions so a glance at Overview covers
    /// every kind of activity, not just workouts.</summary>
    [ObservableProperty] public partial ObservableCollection<NutritionDayRowViewModel> RecentNutritionDays { get; set; } = new();
    [ObservableProperty] public partial ObservableCollection<NutritionDayRowViewModel> RecentStacksTaken { get; set; } = new();
    // No entitlement check here (or on the tabs themselves) — this is read-only history,
    // which stays visible for every member regardless of Full Access. See ShowNutrition/ShowStacksHistory.
    public bool HasRecentNutrition => RecentNutritionDays.Count > 0;
    public bool HasRecentStacksTaken => RecentStacksTaken.Count > 0;
    partial void OnRecentNutritionDaysChanged(ObservableCollection<NutritionDayRowViewModel> value) => OnPropertyChanged(nameof(HasRecentNutrition));
    partial void OnRecentStacksTakenChanged(ObservableCollection<NutritionDayRowViewModel> value) => OnPropertyChanged(nameof(HasRecentStacksTaken));

    [ObservableProperty] public partial string MonthLabel { get; set; } = "";
    [ObservableProperty] public partial ObservableCollection<CalendarCellViewModel> CalendarCells { get; set; } = new();
    [ObservableProperty] public partial ObservableCollection<HistorySessionRowViewModel> RecentSessions { get; set; } = new();

    [ObservableProperty] public partial int AllTimeWorkouts { get; set; }
    [ObservableProperty] public partial int LongestStreak { get; set; }
    [ObservableProperty] public partial string AllTimeVolumeLabel { get; set; } = "";
    [ObservableProperty] public partial ObservableCollection<string> ProgressExercises { get; set; } = new();
    [ObservableProperty] public partial string? SelectedProgressExercise { get; set; }
    [ObservableProperty] public partial WeightTrendDrawable ProgressDrawable { get; set; } = new();
    [ObservableProperty] public partial string ProgressBestLabel { get; set; } = "";
    [ObservableProperty] public partial string ProgressSessionsLabel { get; set; } = "";
    [ObservableProperty] public partial bool HasProgressData { get; set; }

    [ObservableProperty] public partial string LogSearch { get; set; } = "";
    // DatePicker has no concept of "no date selected", so MinValue is the sentinel for "no filter" —
    // the Clear button resets to it. LogDateTo's sentinel is MaxValue for the same reason.
    [ObservableProperty] public partial DateTime LogDateFrom { get; set; } = DateTime.MinValue;
    [ObservableProperty] public partial DateTime LogDateTo { get; set; } = DateTime.MaxValue;
    [ObservableProperty] public partial ObservableCollection<LogRowViewModel> LogRows { get; set; } = new();
    [ObservableProperty] public partial bool LogEmpty { get; set; }
    [ObservableProperty] public partial string ExportStatus { get; set; } = "";

    private LogSortKey _sortKey = LogSortKey.Date;
    private bool _sortDescending = true;

    public HistoryViewModel(IActiveSessionService session, IWorkoutRepository repo, IEntitlementService entitlements)
    {
        _session = session;
        _repo = repo;
        _entitlements = entitlements;
    }

    partial void OnSelectedTabChanged(HistoryTab value)
    {
        OnPropertyChanged(nameof(IsOverviewTab));
        OnPropertyChanged(nameof(IsProgressTab));
        OnPropertyChanged(nameof(IsLogTab));
        OnPropertyChanged(nameof(IsNutritionTab));
        OnPropertyChanged(nameof(IsStacksTab));
        OnPropertyChanged(nameof(IsMeasurementTab));
        if (value == HistoryTab.Log) RebuildLog();
    }
    partial void OnSelectedProgressExerciseChanged(string? value) => RebuildProgressChart();
    partial void OnSelectedMeasurementMetricChanged(string value) => RebuildMeasurementChart();
    partial void OnLogSearchChanged(string value) => RebuildLog();
    partial void OnLogDateFromChanged(DateTime value) => RebuildLog();
    partial void OnLogDateToChanged(DateTime value) => RebuildLog();

    [RelayCommand] private void ShowCalendar() => SelectedTab = HistoryTab.Overview;
    [RelayCommand] private void ShowProgress() => SelectedTab = HistoryTab.Progress;

    /// <summary>Defaults the Log tab's date filter to today whenever you tap into it fresh from
    /// another tab — clicking a specific calendar day (SelectCalendarDay) overrides this by
    /// setting the filter itself and switching tabs directly, bypassing this command.</summary>
    [RelayCommand]
    private void ShowLog()
    {
        LogSearch = "";
        LogDateFrom = DateTime.Today;
        LogDateTo = DateTime.Today;
        SelectedTab = HistoryTab.Log;
    }
    // History is read-only, so both tabs are always reachable regardless of Full
    // Access — a member without it still gets to see their own past nutrition/
    // stacks entries, just not the ability to log new ones (that's gated on the
    // live Nutrition/Stacks pages themselves via HasFullAccess).
    [RelayCommand] private void ShowNutrition() => SelectedTab = HistoryTab.Nutrition;
    [RelayCommand] private void ShowStacksHistory() => SelectedTab = HistoryTab.Stacks;
    [RelayCommand] private void ShowMeasurement() => SelectedTab = HistoryTab.Measurement;
    [RelayCommand] private void ClearLogFilters() { LogSearch = ""; LogDateFrom = DateTime.MinValue; LogDateTo = DateTime.MaxValue; }
    [RelayCommand] private async Task OpenUpgrade(string page) => await Shell.Current.GoToAsync($"upgrade?page={page}");

    [RelayCommand]
    private void SelectMeasurementMetric(string metric)
    {
        SelectedMeasurementMetric = metric;
        foreach (var chip in MeasurementMetricChips) chip.IsSelected = chip.Name == metric;
    }

    [RelayCommand]
    private void PreviousMonth()
    {
        _calendarMonth = _calendarMonth.AddMonths(-1);
        BuildCalendar();
    }

    [RelayCommand]
    private void NextMonth()
    {
        _calendarMonth = _calendarMonth.AddMonths(1);
        BuildCalendar();
    }

    /// <summary>Jumps straight to that day's entries in the Log tab — the calendar's click target.
    /// Uses whichever month is currently displayed (it may not be today's), not DateTime.Today.</summary>
    [RelayCommand]
    private void SelectCalendarDay(string dayText)
    {
        if (string.IsNullOrEmpty(dayText) || !int.TryParse(dayText, out var day)) return;
        var date = new DateTime(_calendarMonth.Year, _calendarMonth.Month, day);
        LogSearch = "";
        LogDateFrom = date;
        LogDateTo = date;
        SelectedTab = HistoryTab.Log;
    }

    /// <summary>Exports exactly what's currently visible — respects the active search/date filters, same as the original design's export menu.</summary>
    private string BuildCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Date,Slot,Exercise,Set,Reps,Weight,Workout");
        foreach (var row in LogRows)
        {
            sb.AppendLine(string.Join(",", CsvField(row.DateLabel), CsvField(row.Ampm), CsvField(row.ExerciseName),
                CsvField(row.Sets), CsvField(row.Reps), CsvField(row.Weight), CsvField(row.RoutineName)));
        }
        return sb.ToString();
    }

    private static string CsvField(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;

    [RelayCommand]
    private async Task CopyLogToClipboard()
    {
        if (LogRows.Count == 0) { ExportStatus = "Nothing to copy."; return; }
        await Clipboard.Default.SetTextAsync(BuildCsv());
        ExportStatus = $"Copied {LogRows.Count} rows to clipboard.";
    }

    [RelayCommand]
    private async Task ShareLogCsv()
    {
        if (LogRows.Count == 0) { ExportStatus = "Nothing to export."; return; }
        try
        {
            var path = Path.Combine(FileSystem.CacheDirectory, "workout-log.csv");
            await File.WriteAllTextAsync(path, BuildCsv());
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Workout Log",
                File = new ShareFile(path),
            });
            ExportStatus = "";
        }
        catch (Exception ex)
        {
            ExportStatus = $"Export failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SortBy(string key)
    {
        var newKey = Enum.Parse<LogSortKey>(key);
        _sortDescending = newKey == _sortKey ? !_sortDescending : true;
        _sortKey = newKey;
        RebuildLog();
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
        var shared = await _repo.GetSharedLibraryAsync(account.Id);
        var manufacturer = await _repo.GetManufacturerLibraryAsync();
        Exercise? FindExercise(Guid id) =>
            shared.Exercises.FirstOrDefault(e => e.Id == id) ?? manufacturer.Exercises.FirstOrDefault(e => e.Id == id);

        HasNutritionAccess = _entitlements.HasPageAccess(account, member.Id, PageEntitlements.Nutrition);
        HasStacksAccess = _entitlements.HasPageAccess(account, member.Id, PageEntitlements.Stacks);
        HasMeasurementsAccess = _entitlements.HasPageAccess(account, member.Id, PageEntitlements.Measurements);

        var memberData = await _repo.GetMemberDataAsync(account.Id, member.Id);
        _completedSessions = memberData.Sessions.Where(s => s.Status == SessionStatus.Completed).ToList();
        _weightUnit = memberData.WeightUnit;
        _measurements = memberData.Measurements;
        _customMeasurementSlots = memberData.CustomMeasurementSlots;

        IEnumerable<SetLogRow> BuildLogRows(WorkoutSession s, SessionExerciseEntry entry)
        {
            var name = entry.Label ?? FindExercise(entry.ExerciseId)?.Name ?? "Exercise";
            SetLogRow Row(int setIndex, string sets, string reps, string weight) => new()
            {
                MemberId = member.Id, Date = s.Date, Slot = s.Slot, RoutineDefinitionId = s.RoutineDefinitionId,
                RoutineName = s.RoutineNameSnapshot, ExerciseId = entry.ExerciseId, ExerciseName = name,
                SetIndex = setIndex, Sets = sets, Reps = reps, Weight = weight,
            };

            if (entry.Vibration is { } vib)
            {
                // Matches the original design's log encoding: level/duration/frequency
                // packed into the same three display columns standard sets use.
                yield return Row(1, vib.Level?.ToString() ?? "", vib.DurationSeconds is int d ? $"{d}s" : "", vib.FrequencyHz is int f ? $"{f}Hz" : "");
                yield break;
            }
            if (entry.CardioTimeMinutes is not null || entry.CardioDistance is not null || entry.CardioResistance is not null)
            {
                yield return Row(1, entry.CardioResistance?.ToString() ?? "", entry.CardioTimeMinutes is int t ? $"{t} min" : "", entry.CardioDistance is double dist ? $"{dist} mi" : "");
                yield break;
            }
            foreach (var (g, gi) in entry.Groups.Select((g, gi) => (g, gi)))
            {
                yield return Row(gi + 1, g.Sets, g.Reps, g.Weight);
            }
        }

        _allLogRows = _completedSessions
            .SelectMany(s => s.Entries.Where(e => e.Done).SelectMany(entry => BuildLogRows(s, entry)))
            .ToList();

        BuildCalendar();
        RebuildLog();
        BuildAllTimeStats();
        BuildProgressData(FindExercise);
        BuildNutritionSummary(memberData.Meals);
        BuildStackHistorySummary(memberData.StackLog, memberData.Stacks);
        BuildMeasurementChips();
    }

    /// <summary>Mirrors MeasurementsViewModel's own metric picker + trend chart —
    /// deliberately the all-time-only subset (no custom date range, no waist/hip
    /// ratio, no progress photos), since this is a compact History tab, not a
    /// replacement for the full Body Measurements page.</summary>
    private void BuildMeasurementChips()
    {
        var allMetrics = BuiltInMeasurementMetrics.Concat(_customMeasurementSlots.Select(s => s.Name)).ToList();
        if (!allMetrics.Contains(SelectedMeasurementMetric)) SelectedMeasurementMetric = "Weight";

        var chips = new ObservableCollection<MetricChipViewModel>();
        foreach (var m in allMetrics) chips.Add(new MetricChipViewModel(m, m == SelectedMeasurementMetric, SelectMeasurementMetricCommand));
        MeasurementMetricChips = chips;

        RebuildMeasurementChart();
    }

    private double? CustomMeasurementValue(BodyMeasurementEntry entry, string slotName)
    {
        var slot = _customMeasurementSlots.FirstOrDefault(s => s.Name == slotName);
        if (slot is null) return null;
        return entry.CustomValues.TryGetValue(slot.Id, out var v) ? v : null;
    }

    private void RebuildMeasurementChart()
    {
        Func<BodyMeasurementEntry, double?> selector = SelectedMeasurementMetric switch
        {
            "Weight" => e => e.Weight,
            "Neck" => e => e.Neck,
            "Shoulder" => e => e.Shoulder,
            "Chest" => e => e.Chest,
            "Waist" => e => e.Waist,
            "Abdomen" => e => e.Abdomen,
            "Hip" => e => e.Hip,
            "L-Bicep" => e => e.LBicep,
            "R-Bicep" => e => e.RBicep,
            "L-Thigh" => e => e.LThigh,
            "R-Thigh" => e => e.RThigh,
            "L-Calf" => e => e.LCalf,
            "R-Calf" => e => e.RCalf,
            _ => e => CustomMeasurementValue(e, SelectedMeasurementMetric),
        };
        var points = _measurements
            .Where(e => selector(e).HasValue)
            .OrderBy(e => e.Date)
            .Select(e => (e.Date, Value: selector(e)!.Value))
            .ToList();

        MeasurementDrawable = new WeightTrendDrawable
        {
            Points = points,
            LineColor = AppColors.Get("ColorAccent"),
            EmptyMessage = "Log a weigh-in to see your trend here.",
        };
        HasMeasurementData = points.Count > 0;

        if (points.Count == 0) { MeasurementBestLabel = ""; return; }
        var unit = SelectedMeasurementMetric == "Weight" ? _weightUnit : "in";
        var latest = points[^1];
        MeasurementBestLabel = $"Latest: {latest.Value:0.#} {unit} on {latest.Date.ToDateTime(TimeOnly.MinValue):MMM d, yyyy}";
    }

    /// <summary>Most recent 30 days that have at least one stack actually marked taken,
    /// newest first — mirrors BuildNutritionSummary's identical shape (reuses
    /// NutritionDayRowViewModel directly rather than a near-duplicate type).</summary>
    private void BuildStackHistorySummary(List<StackLogEntry> stackLog, List<SupplementStack> stacks)
    {
        var stackNames = stacks.ToDictionary(s => s.Id, s => s.Name);
        var rows = new ObservableCollection<NutritionDayRowViewModel>();
        foreach (var day in stackLog.Where(l => l.Taken).GroupBy(l => l.Date).OrderByDescending(g => g.Key).Take(30))
        {
            var names = day.Select(l => stackNames.TryGetValue(l.StackId, out var n) ? n : null)
                .Where(n => n is not null).ToList();
            if (names.Count == 0) continue;
            rows.Add(new NutritionDayRowViewModel(
                day.Key.ToDateTime(TimeOnly.MinValue).ToString("MMM d, yyyy"),
                string.Join(", ", names)));
        }
        StackHistoryDayRows = rows;
        StackHistoryEmpty = StackHistoryDayRows.Count == 0;
        RecentStacksTaken = new ObservableCollection<NutritionDayRowViewModel>(StackHistoryDayRows.Take(5));
    }

    /// <summary>Most recent 30 days that actually have a logged meal, newest first — mirrors RecentSessions' shape for workouts.</summary>
    private void BuildNutritionSummary(List<LoggedMeal> meals)
    {
        // Replacing the whole collection (rather than Clear() + Add() in place) avoids
        // BindableLayout briefly seeing an empty source mid-rebuild — that transient
        // empty state crashes natively inside WinUI's own child-collection handling
        // (see the same fix in WorkoutsViewModel.Rebuild()).
        var nutritionDayRows = new ObservableCollection<NutritionDayRowViewModel>();
        foreach (var day in meals.GroupBy(m => m.Date).OrderByDescending(g => g.Key).Take(30))
        {
            var entries = day.SelectMany(m => m.Entries).ToList();
            if (entries.Count == 0) continue;
            var cal = entries.Sum(e => e.Calories);
            var protein = entries.Sum(e => e.ProteinG);
            var carbs = entries.Sum(e => e.CarbsG);
            var fat = entries.Sum(e => e.FatG);
            nutritionDayRows.Add(new NutritionDayRowViewModel(
                day.Key.ToDateTime(TimeOnly.MinValue).ToString("MMM d, yyyy"),
                $"{cal:0} kcal · {protein:0}p / {carbs:0}c / {fat:0}f"));
        }
        NutritionDayRows = nutritionDayRows;
        NutritionEmpty = NutritionDayRows.Count == 0;
        RecentNutritionDays = new ObservableCollection<NutritionDayRowViewModel>(NutritionDayRows.Take(5));
    }

    private void BuildAllTimeStats()
    {
        AllTimeWorkouts = _completedSessions.Count;
        LongestStreak = ComputeLongestStreak(_completedSessions.Select(s => s.Date).ToHashSet());

        double totalVolume = 0;
        foreach (var s in _completedSessions)
        {
            foreach (var e in s.Entries.Where(e => e.Done))
            {
                foreach (var g in e.Groups)
                {
                    var sets = ParseFirstNumber(g.Sets);
                    var reps = ParseFirstNumber(g.Reps);
                    var weight = ParseFirstNumber(g.Weight);
                    if (sets > 0 && reps > 0 && weight > 0) totalVolume += sets * reps * weight;
                }
            }
        }
        AllTimeVolumeLabel = totalVolume > 0 ? $"{totalVolume:N0} {_weightUnit} total volume lifted" : "No weighted sets logged yet";
    }

    /// <summary>
    /// Per-exercise history of the heaviest weight used each session, driving
    /// the trend chart and personal-record callout. Vibration/Cardio/HIIT
    /// entries have no comparable "weight" concept, so they're excluded.
    /// </summary>
    private void BuildProgressData(Func<Guid, Exercise?> findExercise)
    {
        _exerciseWeightHistory = new Dictionary<string, List<(DateOnly, double)>>();
        foreach (var s in _completedSessions.OrderBy(x => x.Date))
        {
            foreach (var e in s.Entries.Where(e => e.Done && e.Vibration is null
                         && e.CardioTimeMinutes is null && e.CardioDistance is null && e.CardioResistance is null
                         && e.Groups.Count > 0))
            {
                var name = e.Label ?? findExercise(e.ExerciseId)?.Name;
                if (name is null) continue;
                var maxWeight = e.Groups.Select(g => ParseFirstNumber(g.Weight)).DefaultIfEmpty(0).Max();
                if (maxWeight <= 0) continue;
                if (!_exerciseWeightHistory.TryGetValue(name, out var list))
                {
                    list = new List<(DateOnly, double)>();
                    _exerciseWeightHistory[name] = list;
                }
                list.Add((s.Date, maxWeight));
            }
        }

        var previousSelection = SelectedProgressExercise;
        ProgressExercises = new ObservableCollection<string>(_exerciseWeightHistory.Keys.OrderBy(n => n));
        HasProgressData = ProgressExercises.Count > 0;
        SelectedProgressExercise = ProgressExercises.Contains(previousSelection ?? "") ? previousSelection : ProgressExercises.FirstOrDefault();
        RebuildProgressChart();
    }

    private void RebuildProgressChart()
    {
        if (SelectedProgressExercise is null || !_exerciseWeightHistory.TryGetValue(SelectedProgressExercise, out var points) || points.Count == 0)
        {
            ProgressDrawable = new WeightTrendDrawable { LineColor = AppColors.Get("ColorAccent") };
            ProgressBestLabel = "";
            ProgressSessionsLabel = "";
            return;
        }

        ProgressDrawable = new WeightTrendDrawable
        {
            Points = points.Select(p => (p.Date, p.Weight)).ToList(),
            LineColor = AppColors.Get("ColorAccent"),
        };
        var best = points.OrderByDescending(p => p.Weight).First();
        ProgressBestLabel = $"Best: {best.Weight:0.#} {_weightUnit} on {best.Date.ToDateTime(TimeOnly.MinValue):MMM d, yyyy}";
        ProgressSessionsLabel = $"{points.Count} session{(points.Count == 1 ? "" : "s")} logged";
    }

    private static int ComputeLongestStreak(HashSet<DateOnly> dates)
    {
        if (dates.Count == 0) return 0;
        var sorted = dates.OrderBy(d => d).ToList();
        var longest = 1;
        var current = 1;
        for (var i = 1; i < sorted.Count; i++)
        {
            current = sorted[i] == sorted[i - 1].AddDays(1) ? current + 1 : 1;
            longest = Math.Max(longest, current);
        }
        return longest;
    }

    /// <summary>Pulls the leading number out of a field that might be a plain value ("185") or a range ("10-12").</summary>
    private static double ParseFirstNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        var sb = new StringBuilder();
        foreach (var c in value)
        {
            if (char.IsDigit(c) || c == '.') sb.Append(c);
            else if (sb.Length > 0) break;
        }
        return double.TryParse(sb.ToString(), out var d) ? d : 0;
    }

    private void BuildCalendar()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        MonthLabel = _calendarMonth.ToString("MMMM yyyy");
        var loggedDates = _completedSessions.Select(s => s.Date).ToHashSet();

        var firstOfMonth = new DateTime(_calendarMonth.Year, _calendarMonth.Month, 1);
        var startPad = (int)firstOfMonth.DayOfWeek;
        var daysInMonth = DateTime.DaysInMonth(_calendarMonth.Year, _calendarMonth.Month);

        // Replacing the whole collection (rather than Clear() + Add() in place) avoids
        // BindableLayout briefly seeing an empty source mid-rebuild — that transient
        // empty state crashes natively inside WinUI's own child-collection handling
        // (see the same fix in WorkoutsViewModel.Rebuild()).
        var calendarCells = new ObservableCollection<CalendarCellViewModel>();
        for (var i = 0; i < startPad; i++) calendarCells.Add(new CalendarCellViewModel("", false, false, SelectCalendarDayCommand));
        for (var day = 1; day <= daysInMonth; day++)
        {
            var date = DateOnly.FromDateTime(new DateTime(_calendarMonth.Year, _calendarMonth.Month, day));
            calendarCells.Add(new CalendarCellViewModel(day.ToString(), date == today, loggedDates.Contains(date), SelectCalendarDayCommand));
        }
        CalendarCells = calendarCells;

        var recentSessions = new ObservableCollection<HistorySessionRowViewModel>();
        foreach (var s in _completedSessions.OrderByDescending(s => s.Date).Take(10))
        {
            recentSessions.Add(new HistorySessionRowViewModel(
                s.RoutineNameSnapshot,
                s.Date.ToDateTime(TimeOnly.MinValue).ToString("MMM d"),
                s.Entries.Count));
        }
        RecentSessions = recentSessions;
    }

    private void RebuildLog()
    {
        var rows = _allLogRows.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(LogSearch))
        {
            var needle = LogSearch.Trim();
            rows = rows.Where(r =>
                r.ExerciseName.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                r.RoutineName.Contains(needle, StringComparison.OrdinalIgnoreCase));
        }
        if (LogDateFrom != DateTime.MinValue) rows = rows.Where(r => r.Date >= DateOnly.FromDateTime(LogDateFrom));
        if (LogDateTo != DateTime.MaxValue) rows = rows.Where(r => r.Date <= DateOnly.FromDateTime(LogDateTo));

        rows = _sortKey switch
        {
            LogSortKey.Exercise => _sortDescending ? rows.OrderByDescending(r => r.ExerciseName) : rows.OrderBy(r => r.ExerciseName),
            LogSortKey.Weight => _sortDescending ? rows.OrderByDescending(r => ParseWeight(r.Weight)) : rows.OrderBy(r => ParseWeight(r.Weight)),
            _ => _sortDescending ? rows.OrderByDescending(r => r.Date) : rows.OrderBy(r => r.Date),
        };

        LogRows.Clear();
        foreach (var r in rows)
        {
            LogRows.Add(new LogRowViewModel(
                r.Date.ToDateTime(TimeOnly.MinValue).ToString("MMM d"),
                r.Slot == Slot.Am ? "AM" : "PM",
                r.ExerciseName, r.Sets, r.Reps, r.Weight, r.RoutineName));
        }
        LogEmpty = LogRows.Count == 0;
    }

    private static double ParseWeight(string weight) => double.TryParse(weight, out var w) ? w : 0;
}

public class CalendarCellViewModel
{
    public string Day { get; }
    public bool IsToday { get; }
    public bool IsLogged { get; }
    public Color BackgroundColor { get; }
    public ICommand SelectCommand { get; }

    public CalendarCellViewModel(string day, bool isToday, bool isLogged, ICommand selectCommand)
    {
        Day = day;
        IsToday = isToday;
        IsLogged = isLogged;
        BackgroundColor = isLogged ? AppColors.Get("ColorAccent800") : isToday ? AppColors.Get("ColorSurface") : Colors.Transparent;
        SelectCommand = selectCommand;
    }
}

public class HistorySessionRowViewModel
{
    public string RoutineName { get; }
    public string DateLabel { get; }
    public int ExerciseCount { get; }

    public HistorySessionRowViewModel(string routineName, string dateLabel, int exerciseCount)
    {
        RoutineName = routineName;
        DateLabel = dateLabel;
        ExerciseCount = exerciseCount;
    }
}

public class NutritionDayRowViewModel
{
    public string DateLabel { get; }
    public string SummaryLabel { get; }

    public NutritionDayRowViewModel(string dateLabel, string summaryLabel)
    {
        DateLabel = dateLabel;
        SummaryLabel = summaryLabel;
    }
}

public class LogRowViewModel
{
    public string DateLabel { get; }
    public string Ampm { get; }
    public string ExerciseName { get; }
    public string Sets { get; }
    public string Reps { get; }
    public string Weight { get; }
    public string RoutineName { get; }

    public LogRowViewModel(string dateLabel, string ampm, string exerciseName, string sets, string reps, string weight, string routineName)
    {
        DateLabel = dateLabel;
        Ampm = ampm;
        ExerciseName = exerciseName;
        Sets = sets;
        Reps = reps;
        Weight = weight;
        RoutineName = routineName;
    }
}
