using System.Text.Json.Serialization;
using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using WorkoutTracker.Api.Services;
using WorkoutTracker.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.Configure<DriveOptions>(builder.Configuration.GetSection("Drive"));
builder.Services.AddSingleton<IDriveDocumentStore, DriveDocumentStore>();

// The Firebase project's own service account (Secret Manager: firebase-service-account,
// bound to the FirebaseServiceAccountJson env var) — needed to verify ID tokens
// server-side. Every sign-in method (Google, email/password, phone later) all issue
// the same shape of Firebase ID token, so this one client verifies all of them.
FirebaseApp.Create(new AppOptions
{
    Credential = CredentialFactory.FromJson<ServiceAccountCredential>(builder.Configuration["FirebaseServiceAccountJson"]).ToGoogleCredential(),
});
// Minimal API request/response body binding needs the same enum convention
// (strings, not numbers) as the client and DriveDocumentStore use, or a PUT
// carrying any enum field (Slot, SessionStatus, Visibility, ...) fails model
// binding with a 400 before the handler ever runs.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// A coarse "this request came from our own app" gate — it proves the caller holds
// the key shipped inside the client, nothing about which account it may act on. The
// real per-account authorization is AuthorizeAccountAsync below, which every
// /accounts/** endpoint checks separately against a verified Firebase ID token.
var apiKey = app.Configuration["ApiKey"];
app.Use(async (context, next) =>
{
    // The homepage and privacy policy are the two pages Google's own OAuth consent
    // screen (Branding) requires to be publicly reachable before the app can leave
    // "Testing" mode — they have to load with no API key, since nothing but a plain
    // browser (or Google's own verification check) ever requests them.
    var isPublicPage = context.Request.Path.Value is "/" or "/privacy";
    if (isPublicPage || string.IsNullOrEmpty(apiKey) || context.Request.Headers["X-Api-Key"] == apiKey)
    {
        await next();
        return;
    }
    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
});

app.MapGet("/", () => Results.Content(HomepageHtml, "text/html"));
app.MapGet("/privacy", () => Results.Content(PrivacyPolicyHtml, "text/html"));

app.MapGet("/library/manufacturer", async (IDriveDocumentStore store) =>
    Results.Ok(await store.GetAsync<ManufacturerLibrary>("library/manufacturer.json") ?? new ManufacturerLibrary()));

app.MapPut("/library/manufacturer", async (ManufacturerLibrary body, IDriveDocumentStore store) =>
{
    await store.SaveAsync("library/manufacturer.json", body);
    return Results.NoContent();
});

app.MapGet("/library/rigs", async (IDriveDocumentStore store) =>
    Results.Ok(await store.GetAsync<RigCatalog>("library/rigs.json") ?? new RigCatalog()));

app.MapPut("/library/rigs", async (RigCatalog body, IDriveDocumentStore store) =>
{
    await store.SaveAsync("library/rigs.json", body);
    return Results.NoContent();
});

// Verifies the ID token's signature/issuer/audience/expiry against Firebase's own
// public keys — unlike the client's own decode (which deliberately only reads
// claims, see GoogleAuthService.DecodeIdToken's comment), this is the point where
// that token starts authorizing access to cross-device, server-side data, so a real
// signature check is required rather than trusting client-reported claims. Every
// sign-in method issues the same shape of Firebase ID token — a Google sign-in, an
// email/password sign-in, and eventually a phone sign-in all verify the same way,
// unlike the raw-Google-OAuth-token approach this replaced (which needed a separate
// accepted audience per platform's OAuth client).
async Task<FirebaseToken?> VerifyFirebaseIdToken(string idToken)
{
    try
    {
        return await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(idToken);
    }
    catch (FirebaseAuthException)
    {
        return null;
    }
}

// The X-Api-Key check above only proves "a caller holding our app's shared key made
// this request" — it does NOT scope who can read or write which account, since the
// same key ships inside every install. This is the real per-account check: the caller
// must present a verified Firebase ID token (Authorization: Bearer ...) whose Uid
// appears in the account's own Identities list. On first-ever creation of a brand-new
// account (existing index doesn't exist yet), the check falls back to the identity the
// caller is embedding in the new account itself — since the Uid comes from a verified
// token, a caller can only ever create an account that lists their own identity, never
// someone else's.
async Task<(AccountIndex? Index, FirebaseToken? Token, IResult? Error)> AuthorizeAccountAsync(
    Guid accountId, HttpContext context, IDriveDocumentStore store, AccountIndex? bodyForCreation = null)
{
    var header = context.Request.Headers.Authorization.ToString();
    if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        return (null, null, Results.Unauthorized());

    var token = await VerifyFirebaseIdToken(header["Bearer ".Length..].Trim());
    if (token is null) return (null, null, Results.Unauthorized());

    var existing = await store.GetAsync<AccountIndex>($"accounts/{accountId}/index.json");
    var authorizedAgainst = existing?.Identities ?? bodyForCreation?.Identities;
    if (existing is null && bodyForCreation is null) return (null, token, Results.NotFound());
    if (authorizedAgainst is null || !authorizedAgainst.Any(i => i.AuthProviderRef == token.Uid))
        return (null, token, Results.StatusCode(StatusCodes.Status403Forbidden));

    return (existing, token, null);
}

// Granting or revoking someone else's ability to sign in as a member of this
// account is more sensitive than the general "any linked identity may read/
// write this account" check AuthorizeAccountAsync performs — only the
// account's own primary holder may provision or delete a dependent's
// credential. Resolves the holder's Member row, then their Identity, then
// compares its Uid to the caller's verified token.
bool IsHolder(AccountIndex index, FirebaseToken token)
{
    var holder = index.Members.FirstOrDefault(m => m.Id == index.Account.PrimaryHolderMemberId);
    if (holder?.IdentityId is not Guid holderIdentityId) return false;
    var holderIdentity = index.Identities.FirstOrDefault(i => i.Id == holderIdentityId);
    return holderIdentity?.AuthProviderRef == token.Uid;
}

// Shared with /identities/claim below — links a Firebase Uid to accountId in
// the server-side directory used by "sign in on a new device and find my
// account". Idempotent: adding an already-linked account is a no-op.
async Task ClaimUidForAccountAsync(string uid, string? email, Guid accountId, IDriveDocumentStore store)
{
    var directory = await store.GetAsync<IdentityDirectory>("identities/directory.json") ?? new IdentityDirectory();
    var entry = directory.Entries.FirstOrDefault(x => x.AuthProviderRef == uid);
    if (entry is null)
    {
        entry = new IdentityDirectoryEntry { AuthProviderRef = uid, Email = email };
        directory.Entries.Add(entry);
    }
    entry.Email = email;
    if (!entry.AccountIds.Contains(accountId)) entry.AccountIds.Add(accountId);
    await store.SaveAsync("identities/directory.json", directory);
}

app.MapGet("/accounts/{accountId:guid}", async (Guid accountId, HttpContext ctx, IDriveDocumentStore store) =>
{
    var (index, _, error) = await AuthorizeAccountAsync(accountId, ctx, store);
    return error ?? Results.Ok(index);
});

app.MapPut("/accounts/{accountId:guid}", async (Guid accountId, AccountIndex body, HttpContext ctx, IDriveDocumentStore store) =>
{
    var (_, _, error) = await AuthorizeAccountAsync(accountId, ctx, store, bodyForCreation: body);
    if (error is not null) return error;
    await store.SaveAsync($"accounts/{accountId}/index.json", body);
    return Results.NoContent();
});

app.MapGet("/accounts/{accountId:guid}/shared", async (Guid accountId, HttpContext ctx, IDriveDocumentStore store) =>
{
    var (_, _, error) = await AuthorizeAccountAsync(accountId, ctx, store);
    if (error is not null) return error;
    return Results.Ok(await store.GetAsync<SharedLibrary>($"accounts/{accountId}/shared.json") ?? new SharedLibrary());
});

app.MapPut("/accounts/{accountId:guid}/shared", async (Guid accountId, SharedLibrary body, HttpContext ctx, IDriveDocumentStore store) =>
{
    var (_, _, error) = await AuthorizeAccountAsync(accountId, ctx, store);
    if (error is not null) return error;
    await store.SaveAsync($"accounts/{accountId}/shared.json", body);
    return Results.NoContent();
});

app.MapGet("/accounts/{accountId:guid}/members/{memberId:guid}", async (Guid accountId, Guid memberId, HttpContext ctx, IDriveDocumentStore store) =>
{
    var (_, _, error) = await AuthorizeAccountAsync(accountId, ctx, store);
    if (error is not null) return error;
    return Results.Ok(await store.GetAsync<MemberData>($"accounts/{accountId}/members/{memberId}.json") ?? new MemberData());
});

app.MapPut("/accounts/{accountId:guid}/members/{memberId:guid}", async (Guid accountId, Guid memberId, MemberData body, HttpContext ctx, IDriveDocumentStore store) =>
{
    var (_, _, error) = await AuthorizeAccountAsync(accountId, ctx, store);
    if (error is not null) return error;
    await store.SaveAsync($"accounts/{accountId}/members/{memberId}.json", body);
    return Results.NoContent();
});

app.MapDelete("/accounts/{accountId:guid}/members/{memberId:guid}", async (Guid accountId, Guid memberId, HttpContext ctx, IDriveDocumentStore store) =>
{
    var (_, _, error) = await AuthorizeAccountAsync(accountId, ctx, store);
    if (error is not null) return error;
    await DeleteMemberProgressPhotosAsync(accountId, memberId, store);
    await store.DeleteAsync($"accounts/{accountId}/members/{memberId}.json");
    return Results.NoContent();
});

// Provisions a brand-new, independent Firebase account (email+password) for a
// dependent Member who doesn't have one yet, and immediately claims it to
// this account — so that dependent can sign in with these exact credentials
// on their OWN device and land straight in their own member profile (see
// ProfileGateViewModel.CompleteSignInAsync/FindOwnMemberAsync client-side;
// no changes were needed there since this produces exactly the same
// Identity/Member.IdentityId shape LinkGoogleAccount already does). Holder-only:
// this grants someone else access to the account, unlike ordinary read/write.
app.MapPost("/accounts/{accountId:guid}/members/{memberId:guid}/credential",
    async (Guid accountId, Guid memberId, ProvisionCredentialRequest body, HttpContext ctx, IDriveDocumentStore store) =>
{
    var (index, token, error) = await AuthorizeAccountAsync(accountId, ctx, store);
    if (error is not null) return error;
    if (!IsHolder(index!, token!)) return Results.StatusCode(StatusCodes.Status403Forbidden);

    var member = index!.Members.FirstOrDefault(m => m.Id == memberId);
    if (member is null) return Results.NotFound();
    if (member.IdentityId is not null) return Results.Conflict("Member already has a linked identity.");

    UserRecord newUser;
    try
    {
        newUser = await FirebaseAuth.DefaultInstance.CreateUserAsync(new UserRecordArgs
        {
            Email = body.Email, Password = body.Password, EmailVerified = false,
        });
    }
    catch (FirebaseAuthException ex) { return Results.BadRequest(ex.Message); }

    var identity = new Identity { Id = Guid.NewGuid(), Email = body.Email, AuthProviderRef = newUser.Uid, CreatedAt = DateTimeOffset.UtcNow };
    index.Identities.Add(identity);
    member.IdentityId = identity.Id;
    await store.SaveAsync($"accounts/{accountId}/index.json", index);
    await ClaimUidForAccountAsync(newUser.Uid, body.Email, accountId, store);
    // The client needs this Uid back to build a correct local Identity of its own
    // (matching MemberEditViewModel.LinkGoogleAccount's pattern) — its own later
    // SaveAccountIndexAsync/sync-outbox push could otherwise clobber the AuthProviderRef
    // just written above with a client-built Identity that doesn't know it yet.
    return Results.Ok(new ProvisionCredentialResponse(newUser.Uid));
});

// Permanently deletes a dependent's independent sign-in — the real Firebase
// account, this account's own Identity record, and the global directory
// entry — without touching their MemberData/workout history or their roster
// entry (see ManageMembersViewModel.RemoveAsync for the separate, non-
// destructive "remove from roster" action). Holder-only, same reasoning as
// the provisioning endpoint above.
app.MapDelete("/accounts/{accountId:guid}/members/{memberId:guid}/identity",
    async (Guid accountId, Guid memberId, HttpContext ctx, IDriveDocumentStore store) =>
{
    var (index, token, error) = await AuthorizeAccountAsync(accountId, ctx, store);
    if (error is not null) return error;
    if (!IsHolder(index!, token!)) return Results.StatusCode(StatusCodes.Status403Forbidden);

    var member = index!.Members.FirstOrDefault(m => m.Id == memberId);
    if (member?.IdentityId is not Guid identityId) return Results.NotFound();
    var identity = index.Identities.FirstOrDefault(i => i.Id == identityId);

    if (identity?.AuthProviderRef is string uid)
    {
        try { await FirebaseAuth.DefaultInstance.DeleteUserAsync(uid); } catch (FirebaseAuthException) { /* already gone */ }
        var directory = await store.GetAsync<IdentityDirectory>("identities/directory.json");
        var entry = directory?.Entries.FirstOrDefault(e => e.AuthProviderRef == uid);
        if (entry is not null && directory is not null)
        {
            entry.AccountIds.Remove(accountId);
            directory.Entries.RemoveAll(e => e.AccountIds.Count == 0);
            await store.SaveAsync("identities/directory.json", directory);
        }
    }
    if (identity is not null) index.Identities.Remove(identity);
    member.IdentityId = null;
    await store.SaveAsync($"accounts/{accountId}/index.json", index);
    return Results.NoContent();
});

// Progress photo blobs aren't referenced by anything once their owning member's
// data is gone — without this, every deleted member would leave up to 100
// compressed photos behind in Drive storage forever, an invisible, permanent
// leak. Best-effort: a photo blob that fails to delete just lingers, same as
// every other best-effort delete path in this app.
async Task DeleteMemberProgressPhotosAsync(Guid accountId, Guid memberId, IDriveDocumentStore store)
{
    var memberData = await store.GetAsync<MemberData>($"accounts/{accountId}/members/{memberId}.json");
    if (memberData is null) return;
    foreach (var photo in memberData.ProgressPhotos)
    {
        try { await store.DeleteBlobAsync($"accounts/{accountId}/members/{memberId}/photos/{photo.BlobFileName}"); }
        catch { /* best-effort — an orphaned blob is a storage cost, not a correctness issue */ }
    }
}

// Progress photo blobs — same AuthorizeAccountAsync gate as every other
// /accounts/** route, but raw bytes instead of a bound JSON body, since a
// JPEG isn't JSON.
app.MapGet("/accounts/{accountId:guid}/members/{memberId:guid}/photos/{blobFileName}", async (Guid accountId, Guid memberId, string blobFileName, HttpContext ctx, IDriveDocumentStore store) =>
{
    var (_, _, error) = await AuthorizeAccountAsync(accountId, ctx, store);
    if (error is not null) return error;
    var bytes = await store.GetBlobAsync($"accounts/{accountId}/members/{memberId}/photos/{blobFileName}");
    return bytes is null ? Results.NotFound() : Results.Bytes(bytes, "image/jpeg");
});

app.MapPut("/accounts/{accountId:guid}/members/{memberId:guid}/photos/{blobFileName}", async (Guid accountId, Guid memberId, string blobFileName, HttpContext ctx, IDriveDocumentStore store) =>
{
    var (_, _, error) = await AuthorizeAccountAsync(accountId, ctx, store);
    if (error is not null) return error;
    using var ms = new MemoryStream();
    await ctx.Request.Body.CopyToAsync(ms);
    await store.SaveBlobAsync($"accounts/{accountId}/members/{memberId}/photos/{blobFileName}", ms.ToArray(), "image/jpeg");
    return Results.NoContent();
});

app.MapDelete("/accounts/{accountId:guid}/members/{memberId:guid}/photos/{blobFileName}", async (Guid accountId, Guid memberId, string blobFileName, HttpContext ctx, IDriveDocumentStore store) =>
{
    var (_, _, error) = await AuthorizeAccountAsync(accountId, ctx, store);
    if (error is not null) return error;
    await store.DeleteBlobAsync($"accounts/{accountId}/members/{memberId}/photos/{blobFileName}");
    return Results.NoContent();
});

// Permanently deletes an entire account — every member's data, the shared library,
// and the account index itself — plus scrubs this accountId out of every identity
// that pointed to it, so a future sign-in under the same Google/email identity
// creates a genuinely fresh account rather than finding the deleted one. This is
// the in-app account-deletion path required by app store review policies (Apple
// explicitly requires it for any app that supports account creation), and what the
// privacy policy's "contact us to delete your data" promise now actually resolves to.
app.MapDelete("/accounts/{accountId:guid}", async (Guid accountId, HttpContext ctx, IDriveDocumentStore store) =>
{
    var (index, _, error) = await AuthorizeAccountAsync(accountId, ctx, store);
    if (error is not null) return error;

    foreach (var member in index!.Members)
    {
        await DeleteMemberProgressPhotosAsync(accountId, member.Id, store);
        await store.DeleteAsync($"accounts/{accountId}/members/{member.Id}.json");
    }
    await store.DeleteAsync($"accounts/{accountId}/shared.json");
    await store.DeleteAsync($"accounts/{accountId}/index.json");

    var directory = await store.GetAsync<IdentityDirectory>("identities/directory.json");
    if (directory is not null)
    {
        var changed = false;
        foreach (var entry in directory.Entries)
        {
            changed |= entry.AccountIds.Remove(accountId);
        }
        directory.Entries.RemoveAll(e => e.AccountIds.Count == 0);
        if (changed) await store.SaveAsync("identities/directory.json", directory);
    }

    return Results.NoContent();
});

app.MapPost("/identities/lookup", async (IdentityLookupRequest body, IDriveDocumentStore store) =>
{
    var token = await VerifyFirebaseIdToken(body.IdToken);
    if (token is null) return Results.Unauthorized();

    var directory = await store.GetAsync<IdentityDirectory>("identities/directory.json") ?? new IdentityDirectory();
    var entry = directory.Entries.FirstOrDefault(e => e.AuthProviderRef == token.Uid);
    return Results.Ok(new IdentityLookupResponse(entry?.AccountIds ?? new List<Guid>()));
});

app.MapPost("/identities/claim", async (IdentityClaimRequest body, IDriveDocumentStore store) =>
{
    var token = await VerifyFirebaseIdToken(body.IdToken);
    if (token is null) return Results.Unauthorized();

    var email = token.Claims.TryGetValue("email", out var e) ? e?.ToString() : null;
    await ClaimUidForAccountAsync(token.Uid, email, body.AccountId, store);
    return Results.NoContent();
});

app.Run();

record IdentityLookupRequest(string IdToken);
record IdentityLookupResponse(List<Guid> AccountIds);
record IdentityClaimRequest(string IdToken, Guid AccountId);
record ProvisionCredentialRequest(string Email, string Password);
record ProvisionCredentialResponse(string Uid);

partial class Program
{
    // Both pages exist to satisfy Google's OAuth consent screen requirements (a
    // reachable homepage + privacy policy URL) as much as to inform an actual
    // visitor — this API has no other public-facing surface.
    public const string HomepageHtml = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <title>Rig Ritual</title>
        <style>
            body { font-family: system-ui, sans-serif; max-width: 640px; margin: 60px auto; padding: 0 20px; color: #1c1d26; line-height: 1.6; }
            h1 { margin-bottom: 4px; }
            a { color: #5d5294; }
        </style>
        </head>
        <body>
            <h1>Rig Ritual</h1>
            <p>A personal workout, nutrition, and measurement tracking app for families —
            schedule workouts, log sets and meals, and track progress over time, synced
            across your own devices.</p>
            <p><a href="/privacy">Privacy Policy</a></p>
        </body>
        </html>
        """;

    public const string PrivacyPolicyHtml = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <title>Rig Ritual Privacy Policy</title>
        <style>
            body { font-family: system-ui, sans-serif; max-width: 640px; margin: 60px auto; padding: 0 20px; color: #1c1d26; line-height: 1.6; }
            h1 { margin-bottom: 4px; }
            h2 { margin-top: 32px; }
        </style>
        </head>
        <body>
            <h1>Privacy Policy</h1>
            <p>Last updated: September 2026</p>

            <h2>What we collect</h2>
            <p>When you create an account, we collect basic profile information you provide
            (display name, avatar color, and optionally a date of birth) and a sign-in
            identity — either your Google account's email address and a unique identifier,
            or an email address and password you choose. We also store the workout,
            nutrition, and measurement data you log while using the app.</p>

            <h2>How we use it</h2>
            <p>This data is used solely to provide the app's own functionality: saving your
            information and syncing it across your own devices when you sign in. We do not
            sell your data, share it with third parties, or use it for advertising.</p>

            <h2>Where it's stored</h2>
            <p>Your data is stored in a private, app-specific storage area (not a regular
            Google Drive folder you'd see in your own Drive) and a backend service, both
            hosted on Google Cloud.</p>

            <h2>Google Sign-In</h2>
            <p>If you sign in with Google, we receive your name, email address, and a unique
            identifier from Google to verify who you are and find your existing account, if
            any. We don't request access to your Drive, Gmail, or any other Google data.</p>

            <h2>Data deletion</h2>
            <p>To request deletion of your account and data, contact us at the address below.</p>

            <h2>Changes to this policy</h2>
            <p>If this policy changes, the update will be posted here.</p>

            <h2>Contact</h2>
            <p><a href="mailto:bmiceelfagain@gmail.com">bmiceelfagain@gmail.com</a></p>
        </body>
        </html>
        """;
}
