using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth;
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
    var isPublicPage = context.Request.Path.Value is "/" or "/privacy" or "/terms";
    if (isPublicPage || string.IsNullOrEmpty(apiKey) || context.Request.Headers["X-Api-Key"] == apiKey)
    {
        await next();
        return;
    }
    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
});

app.MapGet("/", () => Results.Content(HomepageHtml, "text/html"));
app.MapGet("/privacy", () => Results.Content(PrivacyPolicyHtml, "text/html"));
app.MapGet("/terms", () => Results.Content(TermsOfServiceHtml, "text/html"));

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
// Same value as WorkoutTracker.csproj's <ApplicationId> — one identifier used
// for both the Android package name (Google Play Billing) and, unless Dave's
// App Store Connect setup ends up using a different bundle id, the iOS bundle
// id (Apple App Store Server API) too.
const string AppPackageName = "com.davidsworkoutapp.tracker";

// Google/Apple's own webhook and Pub/Sub payloads are camelCase and outside
// our own ConfigureHttpJsonOptions (that only covers ASP.NET's own request/
// response model binding) — a separate options instance for the manual
// JsonSerializer calls in the two webhook handlers below.
var externalJsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

// Built lazily (not at startup, unlike FirebaseApp.Create) so the API can still
// start and serve every other endpoint before Dave has finished the Play
// Console/Secret Manager setup described in the monetization plan — only a
// purchase-verify or webhook call actually needs these secrets to exist.
GooglePlayPurchaseVerifier? googlePlayVerifier = null;
GooglePlayPurchaseVerifier GetGooglePlayVerifier()
{
    if (googlePlayVerifier is not null) return googlePlayVerifier;
    var serviceAccountJson = app.Configuration["GooglePlayServiceAccountJson"]
        ?? throw new InvalidOperationException("GooglePlayServiceAccountJson is not configured.");
    return googlePlayVerifier = new GooglePlayPurchaseVerifier(serviceAccountJson, AppPackageName);
}

AppleAppStoreVerifier? appleVerifier = null;
AppleAppStoreVerifier GetAppleVerifier()
{
    if (appleVerifier is not null) return appleVerifier;
    var privateKey = app.Configuration["AppleAppStoreConnectPrivateKey"]
        ?? throw new InvalidOperationException("AppleAppStoreConnectPrivateKey is not configured.");
    var keyId = app.Configuration["AppleAppStoreConnectKeyId"]
        ?? throw new InvalidOperationException("AppleAppStoreConnectKeyId is not configured.");
    var issuerId = app.Configuration["AppleAppStoreConnectIssuerId"]
        ?? throw new InvalidOperationException("AppleAppStoreConnectIssuerId is not configured.");
    var sandbox = app.Configuration["AppleAppStoreEnvironment"] != "Production";
    return appleVerifier = new AppleAppStoreVerifier(privateKey, keyId, issuerId, AppPackageName, sandbox);
}

// Shared by /purchases/verify and both webhooks below — the one place a
// verified (status, expiresAt, autoRenewing) result gets folded into
// Account.Purchases and Entitlements/PlanId/etc. get recomputed from it.
void UpsertPurchase(Account account, string productId, string platform, string purchaseToken, string status, DateTimeOffset expiresAt, bool autoRenewing)
{
    var purchase = account.Purchases.FirstOrDefault(p => p.PurchaseToken == purchaseToken)
        ?? account.Purchases.FirstOrDefault(p => p.ProductId == productId && p.Platform == platform);
    if (purchase is null)
    {
        purchase = new PurchaseRecord { ProductId = productId, Platform = platform };
        account.Purchases.Add(purchase);
    }
    purchase.PurchaseToken = purchaseToken;
    purchase.ExpiresAt = expiresAt;
    purchase.AutoRenewing = autoRenewing;
    purchase.Status = status;
    EntitlementCalculator.Recompute(account);
}

// Used by both renewal/cancellation webhooks: a notification only ever carries
// a purchase token, never an accountId, so this resolves the account via the
// purchase-tokens/ lookup, re-verifies the token's REAL current state against
// the store (never trusts the notification payload's own claimed status), and
// saves. Silently returns if the token is unknown (not ours / not verified
// yet) or re-verification fails — webhooks retry on non-2xx, but a permanently
// unverifiable token isn't worth holding up delivery for.
async Task ApplyPurchaseUpdateAsync(string purchaseToken, string platform, IDriveDocumentStore store)
{
    var lookup = await store.GetAsync<PurchaseTokenLookup>(PurchaseTokenLookup.DocumentPath(purchaseToken));
    if (lookup is null) return;

    var index = await store.GetAsync<AccountIndex>($"accounts/{lookup.AccountId}/index.json");
    if (index is null) return;

    bool success; string status; DateTimeOffset expiresAt; bool autoRenewing;
    if (platform == "android")
    {
        var result = await GetGooglePlayVerifier().VerifyAsync(purchaseToken);
        (success, status, expiresAt, autoRenewing) = (result.Success, result.Status, result.ExpiresAt, result.AutoRenewing);
    }
    else
    {
        var result = await GetAppleVerifier().VerifyAsync(purchaseToken);
        (success, status, expiresAt, autoRenewing) = (result.Success, result.Status, result.ExpiresAt, result.AutoRenewing);
    }
    if (!success) return;

    UpsertPurchase(index.Account, lookup.ProductId, platform, purchaseToken, status, expiresAt, autoRenewing);
    await store.SaveAsync($"accounts/{lookup.AccountId}/index.json", index);
}

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

// Real billing, phase 1 (Full Access only — see the monetization plan). The
// client never gets to just claim it bought something: it hands over the raw
// store purchase token, and this endpoint is the only place that decides
// whether Account.Entitlements actually changes, by verifying that token
// against Google/Apple's own servers. Returns the updated Account so the
// client can fold the same fields into its own locally-cached AccountIndex
// (this call goes straight to the server, bypassing the normal sync-outbox —
// see RemoteApiWorkoutRepository.VerifyPurchaseAsync's doc comment — so unlike
// every other account write, nothing else will push this change into the
// client's local copy for it).
app.MapPost("/accounts/{accountId:guid}/purchases/verify", async (Guid accountId, VerifyPurchaseRequest body, HttpContext ctx, IDriveDocumentStore store) =>
{
    var (index, _, error) = await AuthorizeAccountAsync(accountId, ctx, store);
    if (error is not null) return error;

    string status; DateTimeOffset expiresAt; bool autoRenewing;
    if (string.Equals(body.Platform, "android", StringComparison.OrdinalIgnoreCase))
    {
        var result = await GetGooglePlayVerifier().VerifyAsync(body.PurchaseToken);
        if (!result.Success) return Results.BadRequest(new VerifyPurchaseResponse(false, result.Error, null));
        (status, expiresAt, autoRenewing) = (result.Status, result.ExpiresAt, result.AutoRenewing);
    }
    else if (string.Equals(body.Platform, "ios", StringComparison.OrdinalIgnoreCase))
    {
        var result = await GetAppleVerifier().VerifyAsync(body.PurchaseToken);
        if (!result.Success) return Results.BadRequest(new VerifyPurchaseResponse(false, result.Error, null));
        (status, expiresAt, autoRenewing) = (result.Status, result.ExpiresAt, result.AutoRenewing);
    }
    else
    {
        return Results.BadRequest(new VerifyPurchaseResponse(false, $"Unknown platform '{body.Platform}'.", null));
    }

    UpsertPurchase(index!.Account, body.ProductId, body.Platform, body.PurchaseToken, status, expiresAt, autoRenewing);
    await store.SaveAsync($"accounts/{accountId}/index.json", index);
    // So the renewal/cancellation webhooks below can resolve "whose account is
    // this?" later — they only ever receive the raw token, never an accountId.
    await store.SaveAsync(PurchaseTokenLookup.DocumentPath(body.PurchaseToken),
        new PurchaseTokenLookup { AccountId = accountId, ProductId = body.ProductId, Platform = body.Platform });

    return Results.Ok(new VerifyPurchaseResponse(true, null, index.Account));
});

// Google Play Real-time Developer Notifications, delivered as a Pub/Sub push
// request (set up in Play Console → Monetization → a Pub/Sub topic → a push
// subscription pointed at this URL — see the monetization plan's manual
// prerequisites). Authenticity comes from Pub/Sub's own push authentication
// (an OIDC ID token in the Authorization header) — never from the notification
// payload's own claims, which anyone could POST here directly.
app.MapPost("/webhooks/google-play", async (HttpContext ctx, IDriveDocumentStore store) =>
{
    var expectedAudience = app.Configuration["GooglePlayPubSubAudience"];
    var authHeader = ctx.Request.Headers.Authorization.ToString();
    if (string.IsNullOrEmpty(expectedAudience) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        return Results.Unauthorized();

    try
    {
        await GoogleJsonWebSignature.ValidateAsync(authHeader["Bearer ".Length..].Trim(),
            new GoogleJsonWebSignature.ValidationSettings { Audience = new[] { expectedAudience } });
    }
    catch (Exception)
    {
        return Results.Unauthorized();
    }

    PubSubEnvelope? envelope;
    try { envelope = await JsonSerializer.DeserializeAsync<PubSubEnvelope>(ctx.Request.Body, externalJsonOptions); }
    catch (JsonException) { return Results.BadRequest(); }

    // Pub/Sub retries on anything but a 2xx, so an envelope/notification shape
    // we don't recognize (a future notificationType, a test notification) is
    // acked and ignored rather than retried forever.
    if (envelope?.Message?.Data is not string data) return Results.Ok();
    var notification = JsonSerializer.Deserialize<DeveloperNotification>(Encoding.UTF8.GetString(Convert.FromBase64String(data)), externalJsonOptions);
    if (notification?.SubscriptionNotification?.PurchaseToken is not string purchaseToken) return Results.Ok();

    await ApplyPurchaseUpdateAsync(purchaseToken, "android", store);
    return Results.Ok();
});

// Apple App Store Server Notifications V2 — registered in App Store Connect →
// your app → General → App Store Server Notifications, for both Sandbox and
// Production (see the monetization plan's manual prerequisites). Apple POSTs
// straight here (no separate pub/sub layer); authenticity comes from the JWS
// signature on signedPayload itself, verified against Apple's published root
// certificates by AppleAppStoreVerifier.DecodeNotificationAsync.
app.MapPost("/webhooks/apple", async (HttpContext ctx, IDriveDocumentStore store) =>
{
    AppleNotificationEnvelope? envelope;
    try { envelope = await JsonSerializer.DeserializeAsync<AppleNotificationEnvelope>(ctx.Request.Body, externalJsonOptions); }
    catch (JsonException) { return Results.BadRequest(); }
    if (envelope?.SignedPayload is not string signedPayload) return Results.BadRequest();

    var (success, _, decoded) = await GetAppleVerifier().DecodeNotificationAsync(signedPayload);
    // Acked either way — an unverifiable/unrecognized payload isn't something
    // retrying will fix, same reasoning as the Google webhook above.
    if (success && decoded is not null) await ApplyPurchaseUpdateAsync(decoded.PurchaseToken, "ios", store);
    return Results.Ok();
});

app.Run();

record IdentityLookupRequest(string IdToken);
record IdentityLookupResponse(List<Guid> AccountIds);
record IdentityClaimRequest(string IdToken, Guid AccountId);
record ProvisionCredentialRequest(string Email, string Password);
record ProvisionCredentialResponse(string Uid);

record VerifyPurchaseRequest(string ProductId, string Platform, string PurchaseToken);
record VerifyPurchaseResponse(bool Success, string? Error, Account? Account);

// Google Pub/Sub push delivery envelope — see
// https://cloud.google.com/pubsub/docs/push#receive_push. `Data` is the
// developer notification JSON, base64-encoded.
record PubSubEnvelope(PubSubMessage? Message, string? Subscription);
record PubSubMessage(string? Data, string? MessageId, string? PublishTime);
// Google Play Real-time Developer Notifications' own JSON shape — see
// https://developer.android.com/google/play/billing/rtdn-reference.
record DeveloperNotification(int Version, string? PackageName, long EventTimeMillis, SubscriptionNotification? SubscriptionNotification);
record SubscriptionNotification(int Version, int NotificationType, string PurchaseToken, string? SubscriptionId);

record AppleNotificationEnvelope(string? SignedPayload);

partial class Program
{
    // All three pages exist to satisfy Google's OAuth consent screen requirements
    // (a reachable homepage + privacy policy URL) and Apple/Google's subscription
    // disclosure requirements (a Terms of Use link reachable from the purchase
    // screen — see UpgradePage.xaml) as much as to inform an actual visitor — this
    // API has no other public-facing surface. Once rigritual.com is registered and
    // mapped to this Cloud Run service (Dave's manual step), this same page serves
    // at the real domain — no separate marketing site/hosting needed for this
    // "coming soon" pass. A fuller landing page (screenshots, feature pitch,
    // download buttons) is a deliberately separate, later build.
    public const string HomepageHtml = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>Rig Ritual</title>
        <style>
            :root { color-scheme: dark; }
            body {
                font-family: system-ui, sans-serif; margin: 0; min-height: 100vh;
                display: flex; flex-direction: column; align-items: center; justify-content: center;
                background: #06233C; color: #E6F1FB; line-height: 1.6; padding: 20px; box-sizing: border-box;
            }
            h1 { font-size: 40px; margin: 0; color: #ffffff; letter-spacing: 0.5px; }
            .tagline { color: #85B7EB; max-width: 420px; text-align: center; margin: 12px 0 0; }
            .badge {
                margin-top: 28px; font-size: 13px; font-weight: 600; letter-spacing: 1.5px;
                text-transform: uppercase; color: #06233C; background: #00ADFE;
                padding: 6px 16px; border-radius: 999px;
            }
            .links { margin-top: 48px; font-size: 13px; color: #85B7EB; }
            a { color: #85B7EB; }
            a:hover { color: #ffffff; }
        </style>
        </head>
        <body>
            <h1>Rig Ritual</h1>
            <p class="tagline">A personal workout, nutrition, and measurement tracking app for
            families — schedule workouts, log sets and meals, and track progress over time,
            synced across your own devices.</p>
            <span class="badge">Coming soon</span>
            <p class="links">
                <a href="/terms">Terms of Service</a> · <a href="/privacy">Privacy Policy</a> ·
                <a href="mailto:support@rigritual.com">support@rigritual.com</a>
            </p>
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

            <h2>Children's profiles</h2>
            <p>An account holder (a parent or guardian) may add a Child profile for a family
            member under their management. We do not collect a Child profile's sign-in
            identity or contact information beyond what the account holder chooses to enter,
            and a Child profile is only ever reachable through the account holder's own
            account unless the holder chooses to provision that dependent an independent
            sign-in. By creating a Child profile, the account holder confirms they are that
            child's parent or guardian and consents to this policy on the child's behalf.</p>

            <h2>Subscriptions and purchases</h2>
            <p>If you buy a paid plan, the purchase itself is handled entirely by the Google
            Play Store or Apple App Store — we never receive or store your payment card
            details. We do receive a purchase token/receipt from Google or Apple, which we
            use only to verify the purchase and determine which features it unlocks.</p>

            <h2>How we use it</h2>
            <p>This data is used solely to provide the app's own functionality: saving your
            information and syncing it across your own devices when you sign in. We do not
            sell your data or use it for advertising, and the only third-party sharing that
            happens is the crash/error diagnostics described below.</p>

            <h2>Crash and error reports</h2>
            <p>If the app crashes or encounters an error, technical diagnostic information
            (such as a stack trace, device/OS version, and app version) is sent to Sentry, a
            crash-reporting service, so we can identify and fix the problem. These reports do
            not include your workout, nutrition, or measurement data, and do not include a
            screenshot of the screen you were using.</p>

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
            <p><a href="mailto:support@rigritual.com">support@rigritual.com</a></p>

            <p><a href="/terms">Terms of Service</a></p>
        </body>
        </html>
        """;

    // Dev note: the "Governing law" section below is deliberately generic (United
    // States, no specific state) since Dave hasn't confirmed which state's law he
    // wants this tied to (relevant once/if the business is formally incorporated
    // somewhere specific) — tighten it to a specific state if that ever matters.
    public const string TermsOfServiceHtml = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <title>Rig Ritual Terms of Service</title>
        <style>
            body { font-family: system-ui, sans-serif; max-width: 640px; margin: 60px auto; padding: 0 20px; color: #1c1d26; line-height: 1.6; }
            h1 { margin-bottom: 4px; }
            h2 { margin-top: 32px; }
        </style>
        </head>
        <body>
            <h1>Terms of Service</h1>
            <p>Last updated: September 2026</p>

            <h2>Acceptance of terms</h2>
            <p>By creating an account or using Rig Ritual, you agree to these Terms of
            Service and to the <a href="/privacy">Privacy Policy</a>. If you don't agree,
            don't use the app.</p>

            <h2>The service</h2>
            <p>Rig Ritual is a personal workout, nutrition, and measurement tracking app for
            you and the family members you manage, with data synced across your own
            devices. It is not a medical device and does not provide medical advice — see
            "Health and fitness disclaimer" below.</p>

            <h2>Accounts and family members</h2>
            <p>You must provide accurate information when creating an account. An account
            holder may add dependent family member profiles, including Child profiles, and
            is responsible for all activity under every profile on their account, including
            any independent sign-in they choose to grant a dependent. You're responsible for
            keeping your sign-in credentials secure.</p>

            <h2>Subscriptions and billing</h2>
            <p>Some features require a paid subscription. Subscriptions are billed through
            the Google Play Store or Apple App Store, at the price and billing period shown
            at the time of purchase. Subscriptions renew automatically at the end of each
            billing period unless canceled at least 24 hours before renewal. You can manage
            or cancel a subscription anytime in your Google Play or App Store account
            settings — we can't process cancellations or refunds directly, since we never
            receive your payment details (see the Privacy Policy's "Subscriptions and
            purchases" section). Refunds are subject to Google's and Apple's own refund
            policies.</p>

            <h2>Acceptable use</h2>
            <p>Use the app only for its intended purpose of tracking workouts, nutrition, and
            related fitness data for yourself and the family members you manage. Don't
            attempt to disrupt the service, access another account without authorization, or
            use the app for anything unlawful.</p>

            <h2>Your content</h2>
            <p>You keep ownership of the data and photos you log. You're solely responsible
            for what you upload, and you confirm you have the right to upload any photo you
            add (including a family member's progress photo, which you confirm you're
            authorized to store on their behalf).</p>

            <h2>Health and fitness disclaimer</h2>
            <p>Rig Ritual is not a substitute for professional medical, nutritional, or
            fitness advice. Consult a qualified professional before starting a new exercise,
            nutrition, or supplement program, especially if you have an existing health
            condition. You use any workout, nutrition, or supplement information in the app
            at your own risk.</p>

            <h2>Termination</h2>
            <p>You can delete your account at any time from the app's settings, which
            permanently deletes its data as described in the Privacy Policy. We may suspend
            or terminate access to the service if these terms are violated.</p>

            <h2>Disclaimer of warranties</h2>
            <p>The app is provided "as is," without warranties of any kind, express or
            implied, including that it will be uninterrupted, error-free, or fit for a
            particular purpose.</p>

            <h2>Limitation of liability</h2>
            <p>To the fullest extent permitted by law, Rig Ritual's total liability for any
            claim relating to the app is limited to the amount you paid us in the 12 months
            before the claim arose, or $100 if you haven't paid us anything.</p>

            <h2>Governing law</h2>
            <p>These terms are governed by the laws of the United States, without regard to
            its conflict-of-law provisions.</p>

            <h2>Changes to these terms</h2>
            <p>We may update these terms from time to time; continued use of the app after a
            change constitutes acceptance of the updated terms.</p>

            <h2>Contact</h2>
            <p><a href="mailto:support@rigritual.com">support@rigritual.com</a></p>

            <p><a href="/privacy">Privacy Policy</a></p>
        </body>
        </html>
        """;
}
