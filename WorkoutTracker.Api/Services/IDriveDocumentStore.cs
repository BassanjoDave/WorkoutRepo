namespace WorkoutTracker.Api.Services;

/// <summary>
/// One JSON document per named file in the dedicated storage account's Drive
/// appDataFolder — the server-side counterpart to the MAUI app's
/// LocalJsonWorkoutRepository, same document shapes, different physical store.
/// appDataFolder has no real subfolders, so "paths" like
/// "accounts/{id}/index.json" are just the literal file name with slashes in it;
/// Drive treats the name as an opaque string either way.
/// </summary>
public interface IDriveDocumentStore
{
    Task<T?> GetAsync<T>(string name) where T : class;
    Task SaveAsync<T>(string name, T value);
    Task DeleteAsync(string name);

    // Raw bytes with an arbitrary mime type, for content that isn't JSON
    // (progress-photo JPEGs) — same Drive appDataFolder, same by-name lookup,
    // just no JSON (de)serialization step.
    Task<byte[]?> GetBlobAsync(string name);
    Task SaveBlobAsync(string name, byte[] bytes, string contentType);
    Task DeleteBlobAsync(string name);
}
