using System.Text.Json;
using System.Text.Json.Serialization;
using WorkoutTracker.Models;

namespace WorkoutTracker.Services.Storage;

/// <summary>
/// Local-first primary store: one JSON file per document, under
/// FileSystem.AppDataDirectory/store/, mirroring the layout planned for the
/// Drive-backed dev sync target:
///   store/device-index.json
///   store/library/manufacturer.json
///   store/accounts/{accountId}/index.json
///   store/accounts/{accountId}/shared.json
///   store/accounts/{accountId}/members/{memberId}.json
/// A sync service can later read/write the same document shapes against
/// Drive and reconcile into these files — this class never needs to change
/// for that to work, only a second IWorkoutRepository implementation does.
/// </summary>
public class LocalJsonWorkoutRepository : IWorkoutRepository
{
    private readonly string _root;
    private readonly SemaphoreSlim _ioLock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public LocalJsonWorkoutRepository(string? rootOverride = null)
    {
        _root = rootOverride ?? Path.Combine(FileSystem.AppDataDirectory, "store");
    }

    public Task<DeviceIndex> GetDeviceIndexAsync() =>
        ReadAsync<DeviceIndex>(DeviceIndexPath());

    public Task SaveDeviceIndexAsync(DeviceIndex index) =>
        WriteAsync(DeviceIndexPath(), index);

    public async Task<AccountIndex?> GetAccountIndexAsync(Guid accountId)
    {
        var path = AccountIndexPath(accountId);
        return File.Exists(path) ? await ReadAsync<AccountIndex>(path) : null;
    }

    public Task SaveAccountIndexAsync(AccountIndex index) =>
        WriteAsync(AccountIndexPath(index.Account.Id), index);

    public Task<SharedLibrary> GetSharedLibraryAsync(Guid accountId) =>
        ReadAsync<SharedLibrary>(SharedLibraryPath(accountId));

    public Task SaveSharedLibraryAsync(Guid accountId, SharedLibrary library) =>
        WriteAsync(SharedLibraryPath(accountId), library);

    public Task<MemberData> GetMemberDataAsync(Guid accountId, Guid memberId) =>
        ReadAsync<MemberData>(MemberDataPath(accountId, memberId));

    public Task SaveMemberDataAsync(Guid accountId, Guid memberId, MemberData data) =>
        WriteAsync(MemberDataPath(accountId, memberId), data);

    public Task DeleteMemberDataAsync(Guid accountId, Guid memberId)
    {
        var path = MemberDataPath(accountId, memberId);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<ManufacturerLibrary> GetManufacturerLibraryAsync() =>
        ReadAsync<ManufacturerLibrary>(ManufacturerLibraryPath());

    public Task SaveManufacturerLibraryAsync(ManufacturerLibrary library) =>
        WriteAsync(ManufacturerLibraryPath(), library);

    public Task<RigCatalog> GetRigCatalogAsync() =>
        ReadAsync<RigCatalog>(RigCatalogPath());

    public Task SaveRigCatalogAsync(RigCatalog catalog) =>
        WriteAsync(RigCatalogPath(), catalog);

    public async Task<byte[]?> GetProgressPhotoBlobAsync(Guid accountId, Guid memberId, string blobFileName)
    {
        var path = ProgressPhotoPath(accountId, memberId, blobFileName);
        return File.Exists(path) ? await ReadBlobAsync(path) : null;
    }

    public Task SaveProgressPhotoBlobAsync(Guid accountId, Guid memberId, string blobFileName, byte[] jpegBytes) =>
        WriteBlobAsync(ProgressPhotoPath(accountId, memberId, blobFileName), jpegBytes);

    public Task DeleteProgressPhotoBlobAsync(Guid accountId, Guid memberId, string blobFileName)
    {
        var path = ProgressPhotoPath(accountId, memberId, blobFileName);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string DeviceIndexPath() => Path.Combine(_root, "device-index.json");
    private string ManufacturerLibraryPath() => Path.Combine(_root, "library", "manufacturer.json");
    private string RigCatalogPath() => Path.Combine(_root, "library", "rigs.json");
    private string AccountDir(Guid accountId) => Path.Combine(_root, "accounts", accountId.ToString());
    private string AccountIndexPath(Guid accountId) => Path.Combine(AccountDir(accountId), "index.json");
    private string SharedLibraryPath(Guid accountId) => Path.Combine(AccountDir(accountId), "shared.json");
    private string MemberDataPath(Guid accountId, Guid memberId) =>
        Path.Combine(AccountDir(accountId), "members", $"{memberId}.json");
    private string ProgressPhotoPath(Guid accountId, Guid memberId, string blobFileName) =>
        Path.Combine(AccountDir(accountId), "members", memberId.ToString(), "progress-photos", blobFileName);

    private async Task<T> ReadAsync<T>(string path) where T : new()
    {
        await _ioLock.WaitAsync();
        try
        {
            if (!File.Exists(path)) return new T();
            await using var stream = File.OpenRead(path);
            var result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions);
            return result ?? new T();
        }
        finally { _ioLock.Release(); }
    }

    private async Task WriteAsync<T>(string path, T value)
    {
        await _ioLock.WaitAsync();
        try
        {
            var dir = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(dir);
            // Write to a temp file and replace, so a crash mid-write never
            // leaves a half-written JSON document behind for the next read.
            var tempPath = path + ".tmp";
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, value, JsonOptions);
            }
            File.Move(tempPath, path, overwrite: true);
        }
        finally { _ioLock.Release(); }
    }

    // Plain-byte siblings to ReadAsync<T>/WriteAsync<T> — JSON serialization
    // doesn't apply to a JPEG. Same lock and temp-file-then-atomic-move
    // discipline as the JSON path above.
    private async Task<byte[]> ReadBlobAsync(string path)
    {
        await _ioLock.WaitAsync();
        try { return await File.ReadAllBytesAsync(path); }
        finally { _ioLock.Release(); }
    }

    private async Task WriteBlobAsync(string path, byte[] bytes)
    {
        await _ioLock.WaitAsync();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var tempPath = path + ".tmp";
            await File.WriteAllBytesAsync(tempPath, bytes);
            File.Move(tempPath, path, overwrite: true);
        }
        finally { _ioLock.Release(); }
    }
}
