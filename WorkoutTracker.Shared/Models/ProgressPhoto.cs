namespace WorkoutTracker.Models;

/// <summary>
/// One dated progress photo's metadata for one member. The JPEG bytes are NOT
/// stored here — only BlobFileName, which ties this entry to the actual file
/// synced independently via IWorkoutRepository's photo-blob methods (see
/// SyncingWorkoutRepository). Embedding image bytes directly in MemberData
/// would mean every unrelated save of that document (a logged meal, a
/// schedule edit) re-serializes potentially megabytes of image data.
/// </summary>
public class ProgressPhotoEntry
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }

    /// <summary>
    /// File name only (e.g. "{Guid}.jpg"), never a full path — identical
    /// across local disk, the outbox, and the remote blob store. A fresh Guid
    /// is generated per photo and never reused, so an orphaned blob left
    /// behind by a failed delete can never later collide with a different
    /// photo's file.
    /// </summary>
    public string BlobFileName { get; set; } = "";

    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
