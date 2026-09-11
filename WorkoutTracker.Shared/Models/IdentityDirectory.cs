namespace WorkoutTracker.Models;

/// <summary>
/// identities/directory.json — the one global, cross-account document that
/// makes "sign in on a new device" possible: given a Firebase identity's stable
/// Uid, which Account(s) is it already linked to? Everything else in this
/// storage layer is scoped to a single account; this is deliberately the one
/// exception, since discovering the account in the first place has to happen
/// before any account-scoped document can be addressed at all. One Firebase
/// Uid covers every sign-in method linked to it (Google, email/password, phone
/// later) — the method used to sign in on a given device doesn't need to match
/// the one used originally, since Firebase already unified them upstream.
/// Read-modify-write, no locking — acceptable for this app's scale, same
/// honest-simplification precedent as AccountIndex.UpdatedAt's whole-document
/// last-write-wins.
/// </summary>
public class IdentityDirectory
{
    public List<IdentityDirectoryEntry> Entries { get; set; } = new();
}

public class IdentityDirectoryEntry
{
    /// <summary>The Firebase Uid (FirebaseToken.Uid server-side) — stable regardless of
    /// which linked sign-in method (Google, email/password, phone) was used to obtain it.</summary>
    public string AuthProviderRef { get; set; } = "";
    public string? Email { get; set; }

    /// <summary>Usually one, but Identity.cs already anticipates one identity spanning
    /// more than one account (e.g. a teen who's both a family member and their own holder).</summary>
    public List<Guid> AccountIds { get; set; } = new();
}
