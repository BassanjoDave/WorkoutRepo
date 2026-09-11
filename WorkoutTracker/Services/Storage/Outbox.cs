using System.Text.Json;

namespace WorkoutTracker.Services.Storage;

public enum OutboxDocumentKind { AccountIndex, SharedLibrary, MemberData, ProgressPhotoUpload, ProgressPhotoDelete }

public class OutboxEntry
{
    public OutboxDocumentKind Kind { get; set; }
    public Guid AccountId { get; set; }
    public Guid? MemberId { get; set; }
    public DateTimeOffset QueuedAt { get; set; }

    /// <summary>
    /// Set only for ProgressPhotoUpload/ProgressPhotoDelete — lets multiple
    /// different photos for the same member queue independently, unlike the
    /// whole-document kinds above which dedup to one entry per document.
    /// </summary>
    public string? PhotoBlobFileName { get; set; }

    /// <summary>Same-document identity for dedup — queuing the same document twice just keeps one entry.</summary>
    public string Key => PhotoBlobFileName is null
        ? $"{Kind}:{AccountId}:{MemberId}"
        : $"{Kind}:{AccountId}:{MemberId}:{PhotoBlobFileName}";
}

public class OutboxDocument
{
    public List<OutboxEntry> Entries { get; set; } = new();
}

public interface IOutboxStore
{
    Task<OutboxDocument> LoadAsync();
    Task SaveAsync(OutboxDocument document);
}

/// <summary>
/// Purely local, device-specific bookkeeping of "documents this device has
/// edited that haven't been confirmed pushed yet" — never itself synced.
/// </summary>
public class OutboxStore : IOutboxStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public OutboxStore()
    {
        _path = Path.Combine(FileSystem.AppDataDirectory, "store", "outbox.json");
    }

    public async Task<OutboxDocument> LoadAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (!File.Exists(_path)) return new OutboxDocument();
            await using var stream = File.OpenRead(_path);
            return await JsonSerializer.DeserializeAsync<OutboxDocument>(stream, JsonOptions) ?? new OutboxDocument();
        }
        finally { _lock.Release(); }
    }

    public async Task SaveAsync(OutboxDocument document)
    {
        await _lock.WaitAsync();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var tempPath = _path + ".tmp";
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, document, JsonOptions);
            }
            File.Move(tempPath, _path, overwrite: true);
        }
        finally { _lock.Release(); }
    }
}
