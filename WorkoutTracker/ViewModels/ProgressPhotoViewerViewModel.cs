using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// Full-size view of one progress photo, reached from either the
/// Measurements page's own gallery (your own photos, ownerMemberId ==
/// ActiveMember.Id) or the primary holder's read-only
/// ProgressPhotoGalleryPage for a dependent (ownerMemberId != ActiveMember.Id
/// — edit/delete stay hidden in that case). Reached only through an
/// already-gated Measurements page, so — matching LogMeasurementViewModel's
/// convention for the same situation — this does not redundantly re-check
/// PageEntitlements itself, only that an account/member session exists.
/// </summary>
public partial class ProgressPhotoViewerViewModel : ObservableObject, IQueryAttributable
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;

    private Guid _accountId;
    private Guid _ownerMemberId;
    private Guid _photoId;
    private MemberData _memberData = new();
    private ProgressPhotoEntry? _entry;

    [ObservableProperty] public partial ImageSource? FullImage { get; set; }
    [ObservableProperty] public partial DateTime Date { get; set; } = DateTime.Today;
    [ObservableProperty] public partial string Notes { get; set; } = "";
    [ObservableProperty] public partial bool CanEdit { get; set; }

    public ProgressPhotoViewerViewModel(IActiveSessionService session, IWorkoutRepository repo)
    {
        _session = session;
        _repo = repo;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _ownerMemberId = Guid.Parse((string)query["ownerMemberId"]);
        _photoId = Guid.Parse((string)query["photoId"]);
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
        CanEdit = _ownerMemberId == member.Id;

        _memberData = await _repo.GetMemberDataAsync(_accountId, _ownerMemberId);
        _entry = _memberData.ProgressPhotos.FirstOrDefault(p => p.Id == _photoId);
        if (_entry is null)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        Date = _entry.Date.ToDateTime(TimeOnly.MinValue);
        Notes = _entry.Notes ?? "";

        var bytes = await _repo.GetProgressPhotoBlobAsync(_accountId, _ownerMemberId, _entry.BlobFileName);
        FullImage = bytes is null ? null : ImageSource.FromStream(() => new MemoryStream(bytes));
    }

    [RelayCommand]
    private async Task Save()
    {
        if (!CanEdit || _entry is null) return;
        _entry.Date = DateOnly.FromDateTime(Date);
        _entry.Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes;
        await _repo.SaveMemberDataAsync(_accountId, _ownerMemberId, _memberData);
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (!CanEdit || _entry is null) return;
        var confirmed = await Shell.Current.CurrentPage.DisplayAlertAsync("Delete photo",
            "Delete this progress photo? This can't be undone.", "Delete", "Cancel");
        if (!confirmed) return;

        // Metadata removed first, then the blob — the opposite order from
        // adding a photo — so a crash mid-delete never leaves a metadata
        // entry pointing at a blob that no longer exists.
        _memberData.ProgressPhotos.RemoveAll(p => p.Id == _entry.Id);
        await _repo.SaveMemberDataAsync(_accountId, _ownerMemberId, _memberData);
        await _repo.DeleteProgressPhotoBlobAsync(_accountId, _ownerMemberId, _entry.BlobFileName);

        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task Back() => await Shell.Current.GoToAsync("..");
}
