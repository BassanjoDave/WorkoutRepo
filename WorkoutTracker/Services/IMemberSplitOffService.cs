using WorkoutTracker.Models;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.Services;

/// <summary>
/// The junior split-off migration: promotes a Member out of their current
/// Account into a brand-new standalone Account, carrying their full history.
/// This is a re-parent, not a rebuild — the Member's Id never changes, and
/// only rows this member owns are touched. Sibling members' data is never
/// read or rewritten.
/// </summary>
public interface IMemberSplitOffService
{
    Task<Guid> SplitOffAsync(Guid originAccountId, Guid memberId, string newAccountDisplayName);
}

public class MemberSplitOffService : IMemberSplitOffService
{
    private readonly IWorkoutRepository _repo;

    public MemberSplitOffService(IWorkoutRepository repo) => _repo = repo;

    public async Task<Guid> SplitOffAsync(Guid originAccountId, Guid memberId, string newAccountDisplayName)
    {
        var originIndex = await _repo.GetAccountIndexAsync(originAccountId)
            ?? throw new InvalidOperationException("Origin account not found.");
        var member = originIndex.Members.FirstOrDefault(m => m.Id == memberId)
            ?? throw new InvalidOperationException("Member not found in origin account.");

        var newAccountId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        // 1. Create the new account with this member as its primary holder.
        var newAccount = new Account
        {
            Id = newAccountId,
            PrimaryHolderMemberId = member.Id,
            DisplayName = newAccountDisplayName,
            CreatedAt = now,
        };

        // 2. Re-point the member. Id is unchanged — every foreign key
        // elsewhere that references Member.Id by value stays valid.
        member.AccountId = newAccountId;
        member.RolePreset = RolePreset.Owner;
        member.Overrides = null;

        // Carries the member's own linked identity into the new account — without
        // this, the new account's Identities list stays empty and nobody could ever
        // be authorized to read or write it again (the server checks a caller's
        // Firebase Uid against this exact list; see Program.cs's AuthorizeAccountAsync).
        // A member with no identity of their own (a pure dependent) can't be split off
        // into an independently-reachable account at all, since nobody could sign in
        // as them from anywhere.
        var identity = originIndex.Identities.FirstOrDefault(i => i.Id == member.IdentityId);
        var newAccountIndex = new AccountIndex
        {
            Account = newAccount,
            Members = { member },
            Identities = identity is not null ? new List<Identity> { identity } : new List<Identity>(),
        };
        await _repo.SaveAccountIndexAsync(newAccountIndex);

        // 3. Scoped rewrite: only this member's own member-data document
        // moves. Sibling members' documents are never opened.
        var memberData = await _repo.GetMemberDataAsync(originAccountId, memberId);
        memberData.Schedule.AccountId = newAccountId;
        foreach (var session in memberData.Sessions)
        {
            session.AccountId = newAccountId;
        }
        await _repo.SaveMemberDataAsync(newAccountId, memberId, memberData);

        // Photo blobs live under a per-account path
        // (accounts/{accountId}/members/{memberId}/progress-photos/...) and
        // don't move automatically just because the JSON metadata above did —
        // each referenced blob must be explicitly copied to the new account
        // before DeleteMemberDataAsync below removes the origin's copy of
        // both, or every progress photo this member ever took would be
        // silently lost on split-off.
        foreach (var photo in memberData.ProgressPhotos)
        {
            var bytes = await _repo.GetProgressPhotoBlobAsync(originAccountId, memberId, photo.BlobFileName);
            if (bytes is not null) await _repo.SaveProgressPhotoBlobAsync(newAccountId, memberId, photo.BlobFileName, bytes);
        }

        await _repo.DeleteMemberDataAsync(originAccountId, memberId);

        // 4. Account-visible content this member authored travels with them;
        // content they could merely see but didn't author does not.
        var originShared = await _repo.GetSharedLibraryAsync(originAccountId);
        var authoredRoutines = originShared.Routines.Where(r => r.OwnerMemberId == memberId).ToList();
        var authoredExercises = originShared.Exercises.Where(e => e.OwnerMemberId == memberId).ToList();
        foreach (var r in authoredRoutines) r.AccountId = newAccountId;
        foreach (var e in authoredExercises) e.AccountId = newAccountId;

        var newShared = new SharedLibrary { Routines = authoredRoutines, Exercises = authoredExercises };
        await _repo.SaveSharedLibraryAsync(newAccountId, newShared);

        originShared.Routines.RemoveAll(r => r.OwnerMemberId == memberId);
        originShared.Exercises.RemoveAll(e => e.OwnerMemberId == memberId);
        await _repo.SaveSharedLibraryAsync(originAccountId, originShared);

        // 5. Remove the member from the origin roster; everyone else there is untouched.
        originIndex.Members.RemoveAll(m => m.Id == memberId);
        await _repo.SaveAccountIndexAsync(originIndex);

        return newAccountId;
    }
}
