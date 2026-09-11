using WorkoutTracker.Models;

namespace WorkoutTracker.Services.Storage;

/// <summary>
/// Every read/write to persisted app data goes through this interface. No
/// storage-backend type (Drive file IDs, HTTP clients, file paths) may appear
/// in a view model or service outside the Services/Storage folder — swapping
/// the dev-phase Google Drive backend for whatever ships at go-live must be a
/// single new implementation of this interface, nothing else.
/// </summary>
public interface IWorkoutRepository
{
    Task<DeviceIndex> GetDeviceIndexAsync();
    Task SaveDeviceIndexAsync(DeviceIndex index);

    Task<AccountIndex?> GetAccountIndexAsync(Guid accountId);
    Task SaveAccountIndexAsync(AccountIndex index);

    Task<SharedLibrary> GetSharedLibraryAsync(Guid accountId);
    Task SaveSharedLibraryAsync(Guid accountId, SharedLibrary library);

    Task<MemberData> GetMemberDataAsync(Guid accountId, Guid memberId);
    Task SaveMemberDataAsync(Guid accountId, Guid memberId, MemberData data);

    /// <summary>Used by the junior split-off migration once the member's data has been copied to the new account.</summary>
    Task DeleteMemberDataAsync(Guid accountId, Guid memberId);

    Task<ManufacturerLibrary> GetManufacturerLibraryAsync();

    /// <summary>Used by the seed pipeline to publish shipped content — not something end-user actions ever call.</summary>
    Task SaveManufacturerLibraryAsync(ManufacturerLibrary library);

    Task<RigCatalog> GetRigCatalogAsync();

    /// <summary>Used by the seed pipeline to publish shipped content — not something end-user actions ever call.</summary>
    Task SaveRigCatalogAsync(RigCatalog catalog);

    /// <summary>
    /// Progress photo JPEG bytes, keyed by ProgressPhotoEntry.BlobFileName —
    /// deliberately separate from GetMemberDataAsync/SaveMemberDataAsync since
    /// embedding image bytes in that document would mean every unrelated save
    /// re-serializes megabytes of image data. Returns null if the blob isn't
    /// present locally (SyncingWorkoutRepository fetches it from remote lazily
    /// on this call — never eagerly for every photo when MemberData loads).
    /// </summary>
    Task<byte[]?> GetProgressPhotoBlobAsync(Guid accountId, Guid memberId, string blobFileName);

    /// <summary>Saves a newly captured/compressed photo. Immutable once saved — never called again for the same blobFileName.</summary>
    Task SaveProgressPhotoBlobAsync(Guid accountId, Guid memberId, string blobFileName, byte[] jpegBytes);

    Task DeleteProgressPhotoBlobAsync(Guid accountId, Guid memberId, string blobFileName);
}
