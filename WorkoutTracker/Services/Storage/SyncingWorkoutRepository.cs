using System.Collections.Concurrent;
using WorkoutTracker.Models;

namespace WorkoutTracker.Services.Storage;

/// <summary>
/// Local-first with best-effort background sync to the remote API. Local disk
/// is always the source of truth for what the UI reads and writes — every
/// call returns/persists locally first, so the app works fully offline. Sync
/// happens opportunistically alongside that: writes get queued in a local
/// outbox and pushed when possible; reads kick off a background pull that
/// updates local storage for the *next* read if a newer copy exists remotely.
///
/// Conflict resolution is whole-document last-write-wins on UpdatedAt (see
/// the caveat on AccountIndex.UpdatedAt) — the newer of local vs. remote wins
/// outright, no field-level merge. DeviceIndex is never synced; it's
/// inherently local (which accounts/members this specific device knows about).
/// </summary>
public class SyncingWorkoutRepository : IWorkoutRepository
{
    private readonly LocalJsonWorkoutRepository _local;
    private readonly RemoteApiWorkoutRepository _remote;
    private readonly IOutboxStore _outbox;
    private readonly ISyncStatusService _status;
    private readonly SemaphoreSlim _drainLock = new(1, 1);

    // Every GetXAsync call used to fire a brand-new, uncancelled background pull
    // with no dedup — over a long session with many repeated reads (every page
    // OnAppearing calls LoadAsync, which reads), these piled up and contended
    // over the same local file, delaying/starving a *later*, otherwise-correct
    // fresh read behind a backlog of stale in-flight ones. That's what made a
    // just-written change intermittently invisible in the same session even
    // though it was already correctly on disk. Tracking one in-flight pull per
    // document key and skipping redundant ones fixes it at the source.
    private readonly ConcurrentDictionary<string, byte> _pullsInFlight = new();

    public SyncingWorkoutRepository(RemoteApiWorkoutRepository remote, IOutboxStore outbox, ISyncStatusService status)
    {
        _local = new LocalJsonWorkoutRepository();
        _remote = remote;
        _outbox = outbox;
        _status = status;

        Connectivity.ConnectivityChanged += (_, e) =>
        {
            if (e.NetworkAccess == NetworkAccess.Internet) _ = DrainAsync();
        };

        // A push queued before the API key was ever saved (or during a transient
        // outage) otherwise stays stuck forever — nothing else would retry it
        // until the next write or connectivity flip. This is the backstop.
        //
        // Uses Dispatcher.GetForCurrentThread() rather than Application.Current —
        // this singleton is constructed via DI the moment the first page needs a
        // repository, which can happen before Application.Current is guaranteed
        // set on every platform. Application.Current!.Dispatcher crashed the app
        // on first Android launch for exactly this reason; GetForCurrentThread()
        // only needs to be called from the UI thread, which DI construction is.
        var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.GetForCurrentThread();
        if (dispatcher is not null)
        {
            var retryTimer = dispatcher.CreateTimer();
            retryTimer.Interval = TimeSpan.FromSeconds(30);
            retryTimer.Tick += (_, _) => _ = DrainAsync();
            retryTimer.Start();
        }

        _ = DrainAsync();
    }

    public Task<DeviceIndex> GetDeviceIndexAsync() => _local.GetDeviceIndexAsync();
    public Task SaveDeviceIndexAsync(DeviceIndex index) => _local.SaveDeviceIndexAsync(index);

    public async Task<AccountIndex?> GetAccountIndexAsync(Guid accountId)
    {
        var local = await _local.GetAccountIndexAsync(accountId);
        if (local is null)
        {
            // Nothing cached locally at all yet — e.g. a brand-new device that
            // just discovered this account via identity lookup and has never
            // pulled it before. There's no local copy to protect from a stale
            // overwrite here, and a caller with nothing back (ProfileGateViewModel's
            // profile-tile list, notably) has no way to show anything useful
            // while a background pull is still in flight — so fetch synchronously
            // instead of the fire-and-forget pattern below, which assumes there's
            // already something to show meanwhile. Confirmed by testing: signing
            // in on a device with no local cache of an existing account showed a
            // completely empty profile-tile list, because this returned null
            // immediately and the caller had no reason to ever ask again.
            try
            {
                var remote = await _remote.GetAccountIndexAsync(accountId);
                if (remote is not null) await _local.SaveAccountIndexAsync(remote);
                return remote;
            }
            catch { return null; } // offline — caller gets nothing this time, same as before
        }

        _ = TryPullAsync($"account:{accountId}", async () =>
        {
            if (await HasPendingAsync(OutboxDocumentKind.AccountIndex, accountId, null)) return;
            var remote = await _remote.GetAccountIndexAsync(accountId);
            if (remote is null) return;
            // Re-read local fresh right before deciding to overwrite, rather than
            // comparing against the snapshot captured above — that snapshot goes
            // stale the instant a write completes while this pull's network
            // round-trip is still in flight, and would silently clobber the newer
            // write with an older remote copy. Confirmed by testing: entitlement
            // and premium-seat writes reliably persisted to disk but intermittently
            // appeared to "revert" within the same session — this was why.
            var current = await _local.GetAccountIndexAsync(accountId);
            if (current is null || remote.UpdatedAt > current.UpdatedAt)
                await _local.SaveAccountIndexAsync(remote);
        });
        return local;
    }

    public async Task SaveAccountIndexAsync(AccountIndex index)
    {
        index.UpdatedAt = DateTimeOffset.UtcNow;
        await _local.SaveAccountIndexAsync(index);
        await EnqueueAsync(OutboxDocumentKind.AccountIndex, index.Account.Id, null);
        _ = DrainAsync();
    }

    public async Task<SharedLibrary> GetSharedLibraryAsync(Guid accountId)
    {
        var local = await _local.GetSharedLibraryAsync(accountId);
        if (local.UpdatedAt == default)
        {
            // Nothing cached locally yet — unlike GetAccountIndexAsync this never
            // returns null (LocalJsonWorkoutRepository hands back a fresh empty
            // SharedLibrary when the file doesn't exist), so an un-saved default
            // (UpdatedAt never stamped) is the signal instead. Fetching
            // synchronously here matters because an empty library on a brand-new
            // device would otherwise look exactly like "my shared routines and
            // exercises are gone" rather than "still loading."
            try
            {
                var remote = await _remote.GetSharedLibraryAsync(accountId);
                if (remote.UpdatedAt != default) await _local.SaveSharedLibraryAsync(accountId, remote);
                return remote;
            }
            catch { return local; } // offline — nothing to show yet, same as before
        }

        _ = TryPullAsync($"shared:{accountId}", async () =>
        {
            if (await HasPendingAsync(OutboxDocumentKind.SharedLibrary, accountId, null)) return;
            var remote = await _remote.GetSharedLibraryAsync(accountId);
            // Re-read local fresh rather than trusting the snapshot above — see the
            // matching comment on GetAccountIndexAsync for why.
            var current = await _local.GetSharedLibraryAsync(accountId);
            if (remote.UpdatedAt > current.UpdatedAt) await _local.SaveSharedLibraryAsync(accountId, remote);
        });
        return local;
    }

    public async Task SaveSharedLibraryAsync(Guid accountId, SharedLibrary library)
    {
        library.UpdatedAt = DateTimeOffset.UtcNow;
        await _local.SaveSharedLibraryAsync(accountId, library);
        await EnqueueAsync(OutboxDocumentKind.SharedLibrary, accountId, null);
        _ = DrainAsync();
    }

    public async Task<MemberData> GetMemberDataAsync(Guid accountId, Guid memberId)
    {
        var local = await _local.GetMemberDataAsync(accountId, memberId);
        if (local.UpdatedAt == default)
        {
            // Same "nothing cached yet" case as GetSharedLibraryAsync above — an
            // empty MemberData on first view of a member on a new device would
            // look like every workout, meal, and measurement this member ever
            // logged had vanished, rather than "still loading," so this fetches
            // synchronously instead of returning the empty default immediately.
            try
            {
                var remote = await _remote.GetMemberDataAsync(accountId, memberId);
                if (remote.UpdatedAt != default) await _local.SaveMemberDataAsync(accountId, memberId, remote);
                return remote;
            }
            catch { return local; } // offline — nothing to show yet, same as before
        }

        _ = TryPullAsync($"member:{accountId}:{memberId}", async () =>
        {
            if (await HasPendingAsync(OutboxDocumentKind.MemberData, accountId, memberId)) return;
            var remote = await _remote.GetMemberDataAsync(accountId, memberId);
            // Re-read local fresh rather than trusting the snapshot above — see the
            // matching comment on GetAccountIndexAsync for why.
            var current = await _local.GetMemberDataAsync(accountId, memberId);
            if (remote.UpdatedAt > current.UpdatedAt) await _local.SaveMemberDataAsync(accountId, memberId, remote);
        });
        return local;
    }

    public async Task SaveMemberDataAsync(Guid accountId, Guid memberId, MemberData data)
    {
        data.UpdatedAt = DateTimeOffset.UtcNow;
        await _local.SaveMemberDataAsync(accountId, memberId, data);
        await EnqueueAsync(OutboxDocumentKind.MemberData, accountId, memberId);
        _ = DrainAsync();
    }

    public async Task DeleteMemberDataAsync(Guid accountId, Guid memberId)
    {
        // Photo blobs aren't referenced by anything once this member's data
        // document is gone — clean them up here too, or every deleted member
        // leaves up to 100 compressed photos behind forever. Callers that need
        // the blobs to survive (the split-off migration) must copy them to
        // their new home BEFORE calling this, same as it already does for the
        // MemberData document itself.
        var memberData = await _local.GetMemberDataAsync(accountId, memberId);
        foreach (var photo in memberData.ProgressPhotos)
        {
            await _local.DeleteProgressPhotoBlobAsync(accountId, memberId, photo.BlobFileName);
            try { await _remote.DeleteProgressPhotoBlobAsync(accountId, memberId, photo.BlobFileName); }
            catch { /* best-effort — an orphaned remote blob is a storage cost, not a correctness issue */ }
        }

        await _local.DeleteMemberDataAsync(accountId, memberId);
        try { await _remote.DeleteMemberDataAsync(accountId, memberId); }
        catch { /* best-effort — a stale remote copy just lingers until the next successful delete attempt */ }
    }

    // Manufacturer library is static seed content in this dev phase — nothing
    // writes to it per-device, so it isn't wired into the outbox/pull cycle yet.
    public Task<ManufacturerLibrary> GetManufacturerLibraryAsync() => _local.GetManufacturerLibraryAsync();
    public Task SaveManufacturerLibraryAsync(ManufacturerLibrary library) => _local.SaveManufacturerLibraryAsync(library);

    // Same story as the manufacturer library above — a shipped, global catalog,
    // not per-device state, so it isn't wired into the outbox/pull cycle either.
    public Task<RigCatalog> GetRigCatalogAsync() => _local.GetRigCatalogAsync();
    public Task SaveRigCatalogAsync(RigCatalog catalog) => _local.SaveRigCatalogAsync(catalog);

    public async Task<byte[]?> GetProgressPhotoBlobAsync(Guid accountId, Guid memberId, string blobFileName)
    {
        var local = await _local.GetProgressPhotoBlobAsync(accountId, memberId, blobFileName);
        if (local is not null) return local;

        // Deliberately a synchronous fetch-then-cache, not the fire-and-forget
        // TryPullAsync pattern used for JSON documents above: this call is
        // already being awaited by the UI rendering one specific thumbnail
        // that has no local copy yet, so the caller wants the bytes now, not
        // a background refresh of a value it doesn't have.
        try
        {
            var remote = await _remote.GetProgressPhotoBlobAsync(accountId, memberId, blobFileName);
            if (remote is not null) await _local.SaveProgressPhotoBlobAsync(accountId, memberId, blobFileName, remote);
            return remote;
        }
        catch { return null; } // offline or transient error — caller shows a placeholder
    }

    public async Task SaveProgressPhotoBlobAsync(Guid accountId, Guid memberId, string blobFileName, byte[] jpegBytes)
    {
        await _local.SaveProgressPhotoBlobAsync(accountId, memberId, blobFileName, jpegBytes);
        await EnqueueAsync(OutboxDocumentKind.ProgressPhotoUpload, accountId, memberId, blobFileName);
        _ = DrainAsync();
    }

    public async Task DeleteProgressPhotoBlobAsync(Guid accountId, Guid memberId, string blobFileName)
    {
        await _local.DeleteProgressPhotoBlobAsync(accountId, memberId, blobFileName);

        // If an upload for this exact blob never even reached the server yet,
        // just drop it from the outbox instead of queuing a pointless
        // delete-after-an-upload-that-never-happened.
        var outbox = await _outbox.LoadAsync();
        var uploadKey = new OutboxEntry { Kind = OutboxDocumentKind.ProgressPhotoUpload, AccountId = accountId, MemberId = memberId, PhotoBlobFileName = blobFileName }.Key;
        var hadPendingUpload = outbox.Entries.RemoveAll(e => e.Key == uploadKey) > 0;
        await _outbox.SaveAsync(outbox);

        if (!hadPendingUpload)
            await EnqueueAsync(OutboxDocumentKind.ProgressPhotoDelete, accountId, memberId, blobFileName);
        _ = DrainAsync();
    }

    private Task TryPullAsync(string key, Func<Task> pull)
    {
        if (!_pullsInFlight.TryAdd(key, 0)) return Task.CompletedTask;
        _ = Task.Run(async () =>
        {
            try { await pull(); }
            catch { /* offline or transient error — the local copy already returned stands */ }
            finally { _pullsInFlight.TryRemove(key, out _); }
        });
        return Task.CompletedTask;
    }

    private async Task<bool> HasPendingAsync(OutboxDocumentKind kind, Guid accountId, Guid? memberId)
    {
        var outbox = await _outbox.LoadAsync();
        var key = new OutboxEntry { Kind = kind, AccountId = accountId, MemberId = memberId }.Key;
        return outbox.Entries.Any(e => e.Key == key);
    }

    private async Task EnqueueAsync(OutboxDocumentKind kind, Guid accountId, Guid? memberId, string? photoBlobFileName = null)
    {
        var outbox = await _outbox.LoadAsync();
        var entry = new OutboxEntry { Kind = kind, AccountId = accountId, MemberId = memberId, PhotoBlobFileName = photoBlobFileName, QueuedAt = DateTimeOffset.UtcNow };
        outbox.Entries.RemoveAll(e => e.Key == entry.Key);
        outbox.Entries.Add(entry);
        await _outbox.SaveAsync(outbox);
        _status.Report(_status.State == SyncState.Syncing ? SyncState.Syncing : SyncState.Offline, outbox.Entries.Count);
    }

    public async Task DrainAsync()
    {
        if (!await _drainLock.WaitAsync(0)) return;
        try
        {
            var outbox = await _outbox.LoadAsync();
            if (outbox.Entries.Count == 0)
            {
                _status.Report(SyncState.Idle, 0);
                return;
            }

            _status.Report(SyncState.Syncing, outbox.Entries.Count);
            var remaining = new List<OutboxEntry>();
            string? lastError = null;
            foreach (var entry in outbox.Entries.ToList())
            {
                try { await PushOrMergeAsync(entry); }
                catch (Exception ex)
                {
                    remaining.Add(entry);
                    lastError = $"{entry.Kind} {entry.AccountId}: {ex.Message}";
                }
            }
            outbox.Entries = remaining;
            await _outbox.SaveAsync(outbox);
            _status.Report(remaining.Count == 0 ? SyncState.Idle : SyncState.Offline, remaining.Count, lastError);
        }
        finally { _drainLock.Release(); }
    }

    private async Task PushOrMergeAsync(OutboxEntry entry)
    {
        switch (entry.Kind)
        {
            case OutboxDocumentKind.AccountIndex:
                var localAccount = await _local.GetAccountIndexAsync(entry.AccountId);
                if (localAccount is null) return;
                var remoteAccount = await _remote.GetAccountIndexAsync(entry.AccountId);
                // Re-read local again right before deciding, rather than trusting
                // localAccount above — the remote fetch is a real network round-trip,
                // and a newer local write can land while it's in flight. Comparing
                // against a stale snapshot here would push (or accept) the wrong copy.
                var currentAccount = await _local.GetAccountIndexAsync(entry.AccountId);
                if (currentAccount is null) return;
                if (remoteAccount is not null && remoteAccount.UpdatedAt > currentAccount.UpdatedAt)
                    await _local.SaveAccountIndexAsync(remoteAccount);
                else
                    await _remote.SaveAccountIndexAsync(currentAccount);
                break;

            case OutboxDocumentKind.SharedLibrary:
                var localShared = await _local.GetSharedLibraryAsync(entry.AccountId);
                var remoteShared = await _remote.GetSharedLibraryAsync(entry.AccountId);
                var currentShared = await _local.GetSharedLibraryAsync(entry.AccountId);
                if (remoteShared.UpdatedAt > currentShared.UpdatedAt)
                    await _local.SaveSharedLibraryAsync(entry.AccountId, remoteShared);
                else
                    await _remote.SaveSharedLibraryAsync(entry.AccountId, currentShared);
                break;

            case OutboxDocumentKind.MemberData:
                var memberId = entry.MemberId!.Value;
                var localMember = await _local.GetMemberDataAsync(entry.AccountId, memberId);
                var remoteMember = await _remote.GetMemberDataAsync(entry.AccountId, memberId);
                var currentMember = await _local.GetMemberDataAsync(entry.AccountId, memberId);
                if (remoteMember.UpdatedAt > currentMember.UpdatedAt)
                    await _local.SaveMemberDataAsync(entry.AccountId, memberId, remoteMember);
                else
                    await _remote.SaveMemberDataAsync(entry.AccountId, memberId, currentMember);
                break;

            // Photo blobs are push-once-immutable, not whole-document
            // last-write-wins like the three cases above — a photo never
            // mutates after creation, only gets deleted, so there's no
            // re-read-remote-before-deciding dance needed here.
            case OutboxDocumentKind.ProgressPhotoUpload:
                var bytes = await _local.GetProgressPhotoBlobAsync(entry.AccountId, entry.MemberId!.Value, entry.PhotoBlobFileName!);
                if (bytes is null) return; // deleted locally before it was ever pushed — nothing to do
                await _remote.SaveProgressPhotoBlobAsync(entry.AccountId, entry.MemberId.Value, entry.PhotoBlobFileName!, bytes);
                break;

            case OutboxDocumentKind.ProgressPhotoDelete:
                await _remote.DeleteProgressPhotoBlobAsync(entry.AccountId, entry.MemberId!.Value, entry.PhotoBlobFileName!);
                break;
        }
    }
}
