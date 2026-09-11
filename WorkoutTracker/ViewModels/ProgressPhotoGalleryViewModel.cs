using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// The primary holder's separate, read-only entry point into a dependent's
/// progress photos — reached only from the "View Photos" button in Manage
/// Members, which itself is only shown when
/// Member.EffectiveProgressPhotosVisibleToHolder is true for that member.
/// Re-checks that same condition here as a courtesy against a stale link
/// (e.g. the toggle flipped off in another tab), not as a security boundary —
/// see the Progress Photos plan's note that this whole feature is a
/// family-trust UI convenience, consistent with how every other screen in
/// this app already lets a holder reach any member's data.
/// </summary>
public partial class ProgressPhotoGalleryViewModel : ObservableObject, IQueryAttributable
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;

    private Guid _accountId;
    private Guid _ownerMemberId;

    [ObservableProperty] public partial string OwnerDisplayName { get; set; } = "";
    [ObservableProperty] public partial ObservableCollection<ProgressPhotoThumbnailViewModel> ProgressPhotos { get; set; } = new();

    public ProgressPhotoGalleryViewModel(IActiveSessionService session, IWorkoutRepository repo)
    {
        _session = session;
        _repo = repo;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _ownerMemberId = Guid.Parse((string)query["ownerMemberId"]);
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

        var accountIndex = await _repo.GetAccountIndexAsync(account.Id);
        var owner = accountIndex?.Members.FirstOrDefault(m => m.Id == _ownerMemberId);
        if (owner is null || member.Id != account.PrimaryHolderMemberId || !owner.EffectiveProgressPhotosVisibleToHolder)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }
        OwnerDisplayName = owner.DisplayName;

        var memberData = await _repo.GetMemberDataAsync(_accountId, _ownerMemberId);
        var ordered = memberData.ProgressPhotos.OrderByDescending(p => p.Date).ToList();
        var thumbnails = new ObservableCollection<ProgressPhotoThumbnailViewModel>();
        foreach (var photo in ordered)
        {
            thumbnails.Add(new ProgressPhotoThumbnailViewModel(photo, _accountId, _ownerMemberId, _repo, ViewProgressPhotoCommand));
        }
        ProgressPhotos = thumbnails;
    }

    [RelayCommand]
    private async Task ViewProgressPhoto(ProgressPhotoThumbnailViewModel photo) =>
        await Shell.Current.GoToAsync($"progressPhotoViewer?ownerMemberId={_ownerMemberId}&photoId={photo.Id}");

    [RelayCommand]
    private async Task Back() => await Shell.Current.GoToAsync("..");
}
