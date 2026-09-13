using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Controls;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// Body measurement trend tracking — split out of History's Progress tab into
/// its own page so it isn't tied to workout history specifically. Covers the
/// full built-in body-part list plus whatever custom measurement slots the
/// member has defined (see LogMeasurementViewModel).
/// </summary>
public partial class MeasurementsViewModel : ObservableObject
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;
    private readonly IEntitlementService _entitlements;
    private readonly IProgressPhotoCaptureService _photoCapture;

    private List<BodyMeasurementEntry> _measurements = new();
    private List<CustomMeasurementSlot> _customSlots = new();
    private string _weightUnit = "lbs";

    private Guid _accountId;
    private Guid _memberId;
    private const int ProgressPhotoCap = 100;
    private const int ProgressPhotoGridColumns = 3;
    private const double ProgressPhotoThumbnailHeight = 110;
    private const double ProgressPhotoGridSpacing = 6;

    [ObservableProperty] public partial ObservableCollection<ProgressPhotoThumbnailViewModel> ProgressPhotos { get; set; } = new();
    [ObservableProperty] public partial bool IsAtPhotoCap { get; set; }
    [ObservableProperty] public partial double ProgressPhotosGridHeight { get; set; }
    [ObservableProperty] public partial bool HasFullAccess { get; set; }

    // Order matches the log page's layout groupings.
    private static readonly string[] BuiltInMetrics =
        { "Weight", "Neck", "Shoulder", "Chest", "Waist", "Abdomen", "Hip", "L-Bicep", "R-Bicep", "L-Thigh", "R-Thigh", "L-Calf", "R-Calf" };

    [ObservableProperty] public partial ObservableCollection<MetricChipViewModel> MetricChips { get; set; } = new();
    [ObservableProperty] public partial string SelectedBodyMetric { get; set; } = "Weight";
    [ObservableProperty] public partial WeightTrendDrawable BodyMetricDrawable { get; set; } = new();
    [ObservableProperty] public partial string BodyMetricLabel { get; set; } = "";
    [ObservableProperty] public partial bool HasBodyMeasurements { get; set; }
    [ObservableProperty] public partial string WaistHipRatioLabel { get; set; } = "";

    private enum RangeOption { Week, Month, Year, All, Custom }
    private RangeOption _selectedRange = RangeOption.All;
    private static readonly string[] RangeLabels = { "Week", "Month", "Year", "All", "Custom" };

    [ObservableProperty] public partial ObservableCollection<MetricChipViewModel> RangeChips { get; set; } = new();
    [ObservableProperty] public partial bool IsCustomRange { get; set; }
    [ObservableProperty] public partial DateTime CustomFrom { get; set; } = DateTime.Today.AddMonths(-1);
    [ObservableProperty] public partial DateTime CustomTo { get; set; } = DateTime.Today;

    public MeasurementsViewModel(IActiveSessionService session, IWorkoutRepository repo, IEntitlementService entitlements, IProgressPhotoCaptureService photoCapture)
    {
        _session = session;
        _repo = repo;
        _entitlements = entitlements;
        _photoCapture = photoCapture;
    }

    partial void OnSelectedBodyMetricChanged(string value) => RebuildBodyMetricChart();

    public async Task LoadAsync()
    {
        var account = _session.ActiveAccount;
        var member = _session.ActiveMember;
        if (account is null || member is null)
        {
            await Shell.Current.GoToAsync("//gate");
            return;
        }
        // Without Full Access, history still loads and displays normally below —
        // only logging a new weigh-in/photo is gated (see HasFullAccess and the
        // upgrade nudge in MeasurementsPage.xaml).
        HasFullAccess = _entitlements.HasPageAccess(account, member.Id, PageEntitlements.Measurements);

        _accountId = account.Id;
        _memberId = member.Id;

        var memberData = await _repo.GetMemberDataAsync(account.Id, member.Id);
        _measurements = memberData.Measurements;
        _customSlots = memberData.CustomMeasurementSlots;
        _weightUnit = memberData.WeightUnit;
        LoadProgressPhotos(memberData.ProgressPhotos);

        var allMetrics = BuiltInMetrics.Concat(_customSlots.Select(s => s.Name)).ToList();
        var previousSelection = SelectedBodyMetric;
        SelectedBodyMetric = allMetrics.Contains(previousSelection) ? previousSelection : "Weight";

        // Replacing the whole collection (rather than Clear() + Add() in place) avoids
        // BindableLayout briefly seeing an empty source mid-rebuild — that transient
        // empty state crashes natively inside WinUI's own child-collection handling
        // (see the same fix in WorkoutsViewModel.Rebuild()).
        var metricChips = new ObservableCollection<MetricChipViewModel>();
        foreach (var m in allMetrics) metricChips.Add(new MetricChipViewModel(m, m == SelectedBodyMetric, SelectMetricCommand));
        MetricChips = metricChips;

        if (RangeChips.Count == 0)
        {
            foreach (var r in RangeLabels) RangeChips.Add(new MetricChipViewModel(r, r == RangeLabels[3], SelectRangeCommand));
        }

        RebuildWaistHipRatio();
        RebuildBodyMetricChart();
    }

    [RelayCommand]
    private void SelectMetric(string metric)
    {
        SelectedBodyMetric = metric;
        foreach (var chip in MetricChips) chip.IsSelected = chip.Name == metric;
    }

    [RelayCommand]
    private void SelectRange(string range)
    {
        _selectedRange = Enum.Parse<RangeOption>(range);
        IsCustomRange = _selectedRange == RangeOption.Custom;
        foreach (var chip in RangeChips) chip.IsSelected = chip.Name == range;
        RebuildBodyMetricChart();
    }

    partial void OnCustomFromChanged(DateTime value)
    {
        if (_selectedRange == RangeOption.Custom) RebuildBodyMetricChart();
    }

    partial void OnCustomToChanged(DateTime value)
    {
        if (_selectedRange == RangeOption.Custom) RebuildBodyMetricChart();
    }

    private IEnumerable<BodyMeasurementEntry> MeasurementsInRange()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var from = _selectedRange switch
        {
            RangeOption.Week => today.AddDays(-7),
            RangeOption.Month => today.AddMonths(-1),
            RangeOption.Year => today.AddYears(-1),
            RangeOption.Custom => DateOnly.FromDateTime(CustomFrom),
            _ => DateOnly.MinValue,
        };
        var to = _selectedRange == RangeOption.Custom ? DateOnly.FromDateTime(CustomTo) : today;
        return _measurements.Where(m => m.Date >= from && m.Date <= to);
    }

    [RelayCommand]
    private async Task LogMeasurement() => await Shell.Current.GoToAsync("logMeasurement");

    [RelayCommand]
    private async Task OpenUpgrade() => await Shell.Current.GoToAsync($"upgrade?page={PageEntitlements.Measurements}");

    private void LoadProgressPhotos(List<ProgressPhotoEntry> photos)
    {
        // Replacing the whole collection (rather than Clear() + Add() in place)
        // avoids the empty-collection-mid-rebuild WinUI crash noted elsewhere on
        // this page (MetricChips) and in WorkoutsViewModel.Rebuild().
        var ordered = photos.OrderByDescending(p => p.Date).ToList();
        var thumbnails = new ObservableCollection<ProgressPhotoThumbnailViewModel>();
        foreach (var photo in ordered)
        {
            thumbnails.Add(new ProgressPhotoThumbnailViewModel(photo, _accountId, _memberId, _repo, ViewProgressPhotoCommand));
        }
        ProgressPhotos = thumbnails;
        IsAtPhotoCap = ordered.Count >= ProgressPhotoCap;

        var rows = Math.Max(1, (int)Math.Ceiling(ordered.Count / (double)ProgressPhotoGridColumns));
        ProgressPhotosGridHeight = ordered.Count == 0
            ? 0
            : rows * ProgressPhotoThumbnailHeight + (rows - 1) * ProgressPhotoGridSpacing;
    }

    [RelayCommand]
    private async Task AddProgressPhoto()
    {
        if (ProgressPhotos.Count >= ProgressPhotoCap)
        {
            await Shell.Current.CurrentPage.DisplayAlertAsync("Photo limit reached",
                "You've reached the 100-photo limit. Delete an old photo before adding a new one.", "OK");
            return;
        }

        var bytes = await _photoCapture.CaptureAsync();
        if (bytes is null) return;

        // Re-check against a freshly-reloaded document, not the in-memory
        // snapshot above — another device may have pushed photos in between.
        var memberData = await _repo.GetMemberDataAsync(_accountId, _memberId);
        if (memberData.ProgressPhotos.Count >= ProgressPhotoCap)
        {
            await Shell.Current.CurrentPage.DisplayAlertAsync("Photo limit reached",
                "You've reached the 100-photo limit. Delete an old photo before adding a new one.", "OK");
            return;
        }

        var entry = new ProgressPhotoEntry
        {
            Id = Guid.NewGuid(),
            Date = DateOnly.FromDateTime(DateTime.Today),
            BlobFileName = $"{Guid.NewGuid()}.jpg",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        // Blob first, then metadata: if the app is interrupted between the
        // two, the worst case is an orphaned, unreferenced blob (harmless,
        // invisible to the UI) — never a metadata entry pointing at a blob
        // that doesn't exist.
        await _repo.SaveProgressPhotoBlobAsync(_accountId, _memberId, entry.BlobFileName, bytes);
        memberData.ProgressPhotos.Add(entry);
        await _repo.SaveMemberDataAsync(_accountId, _memberId, memberData);

        LoadProgressPhotos(memberData.ProgressPhotos);
    }

    [RelayCommand]
    private async Task ViewProgressPhoto(ProgressPhotoThumbnailViewModel photo) =>
        await Shell.Current.GoToAsync($"progressPhotoViewer?ownerMemberId={_memberId}&photoId={photo.Id}");

    private void RebuildWaistHipRatio()
    {
        var latest = _measurements
            .Where(e => e.Waist.HasValue && e.Hip.HasValue)
            .OrderByDescending(e => e.Date)
            .FirstOrDefault();
        WaistHipRatioLabel = latest is null
            ? ""
            : $"Waist-to-Hip Ratio: {latest.Waist!.Value / latest.Hip!.Value:0.00} (as of {latest.Date.ToDateTime(TimeOnly.MinValue):MMM d, yyyy})";
    }

    private double? CustomValue(BodyMeasurementEntry entry, string metricName)
    {
        var slot = _customSlots.FirstOrDefault(s => s.Name == metricName);
        if (slot is null) return null;
        return entry.CustomValues.TryGetValue(slot.Id, out var v) ? v : null;
    }

    private void RebuildBodyMetricChart()
    {
        Func<BodyMeasurementEntry, double?> selector = SelectedBodyMetric switch
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
            _ => e => CustomValue(e, SelectedBodyMetric),
        };
        var points = MeasurementsInRange()
            .Where(e => selector(e).HasValue)
            .OrderBy(e => e.Date)
            .Select(e => (e.Date, Value: selector(e)!.Value))
            .ToList();

        BodyMetricDrawable = new WeightTrendDrawable
        {
            Points = points,
            LineColor = AppColors.Get("ColorAccent"),
            EmptyMessage = "Log a weigh-in to see your trend here.",
        };
        HasBodyMeasurements = points.Count > 0;

        if (points.Count == 0)
        {
            BodyMetricLabel = "";
            return;
        }
        var unit = SelectedBodyMetric == "Weight" ? _weightUnit : "in";
        var latest = points[^1];
        BodyMetricLabel = $"Latest: {latest.Value:0.#} {unit} on {latest.Date.ToDateTime(TimeOnly.MinValue):MMM d, yyyy}";
    }
}

public partial class MetricChipViewModel : ObservableObject
{
    public string Name { get; }
    public IRelayCommand<string> SelectCommand { get; }

    [ObservableProperty] public partial bool IsSelected { get; set; }

    public MetricChipViewModel(string name, bool isSelected, IRelayCommand<string> selectCommand)
    {
        Name = name;
        IsSelected = isSelected;
        SelectCommand = selectCommand;
    }
}

/// <summary>One thumbnail in the Progress Photos grid. Thumbnail is a lazy
/// ImageSource — combined with CollectionView's own virtualization, this is
/// what makes "fetch a photo's bytes when it's actually rendered, not all up
/// front" true, since GetProgressPhotoBlobAsync only runs when something
/// asks this ImageSource to actually load.</summary>
public class ProgressPhotoThumbnailViewModel
{
    public Guid Id { get; }
    public string DateLabel { get; }
    public ImageSource Thumbnail { get; }
    public IAsyncRelayCommand<ProgressPhotoThumbnailViewModel> ViewCommand { get; }

    public ProgressPhotoThumbnailViewModel(ProgressPhotoEntry entry, Guid accountId, Guid memberId, WorkoutTracker.Services.Storage.IWorkoutRepository repo, IAsyncRelayCommand<ProgressPhotoThumbnailViewModel> viewCommand)
    {
        Id = entry.Id;
        DateLabel = entry.Date.ToDateTime(TimeOnly.MinValue).ToString("MMM d, yyyy");
        ViewCommand = viewCommand;
        Thumbnail = ImageSource.FromStream(async _ =>
        {
            var bytes = await repo.GetProgressPhotoBlobAsync(accountId, memberId, entry.BlobFileName);
            return bytes is null ? null : new MemoryStream(bytes);
        });
    }
}
