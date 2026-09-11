using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using WorkoutTracker.Models;

namespace WorkoutTracker.Services.Storage;

/// <summary>
/// Calls the deployed thin API instead of local disk. This is the entire
/// swap the repository interface was built for — no view model or service
/// outside this file changes when the backend moves from local JSON to this.
///
/// DeviceIndex stays local regardless of backend: it's "which accounts/members
/// this specific device knows about," never something Drive or the API needs
/// an opinion on, so it's delegated to a LocalJsonWorkoutRepository scoped to
/// just that one file.
///
/// What this class does NOT yet do: retry on failure, queue writes made while
/// offline, or resolve conflicts between two devices editing the same record.
/// That's the outbox/sync work still ahead of switching the app's default
/// repository over to this one.
/// </summary>
public class RemoteApiWorkoutRepository : IWorkoutRepository
{
    private readonly HttpClient _http;
    private readonly IFirebaseAuthService _firebaseAuth;
    private readonly LocalJsonWorkoutRepository _localDeviceIndex = new();
    // PropertyNameCaseInsensitive matters here specifically: ASP.NET Core's
    // Minimal API serializes responses with camelCase property names by
    // default (framework default, not something Program.cs opts into), but
    // System.Text.Json's own default deserialization is case-SENSITIVE. Without
    // this, every field silently binds to its type's default (Guid.Empty,
    // empty lists, DateTimeOffset.MinValue) instead of throwing — a real GET
    // response with real data deserializes into a blank object with no error
    // at all. This was invisible all along because the only place a remote
    // fetch's result was ever actually used was a `remote.UpdatedAt >
    // current.UpdatedAt` comparison — a blank object's UpdatedAt is always the
    // oldest possible value, so it always silently lost that comparison and
    // was discarded. It only surfaced once a cold-start fetch (nothing cached
    // locally yet) started returning that blank object directly instead of
    // just comparing it.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public RemoteApiWorkoutRepository(HttpClient http, IFirebaseAuthService firebaseAuth)
    {
        _http = http;
        _firebaseAuth = firebaseAuth;
        _http.BaseAddress = new Uri(RemoteApiConfig.BaseUrl);
    }

    public Task<DeviceIndex> GetDeviceIndexAsync() => _localDeviceIndex.GetDeviceIndexAsync();
    public Task SaveDeviceIndexAsync(DeviceIndex index) => _localDeviceIndex.SaveDeviceIndexAsync(index);

    public async Task<AccountIndex?> GetAccountIndexAsync(Guid accountId)
    {
        using var response = await SendAsync(HttpMethod.Get, $"accounts/{accountId}");
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AccountIndex>(JsonOptions);
    }

    public async Task SaveAccountIndexAsync(AccountIndex index)
    {
        using var response = await SendAsync(HttpMethod.Put, $"accounts/{index.Account.Id}", index);
        response.EnsureSuccessStatusCode();
    }

    public async Task<SharedLibrary> GetSharedLibraryAsync(Guid accountId)
    {
        using var response = await SendAsync(HttpMethod.Get, $"accounts/{accountId}/shared");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SharedLibrary>(JsonOptions) ?? new SharedLibrary();
    }

    public async Task SaveSharedLibraryAsync(Guid accountId, SharedLibrary library)
    {
        using var response = await SendAsync(HttpMethod.Put, $"accounts/{accountId}/shared", library);
        response.EnsureSuccessStatusCode();
    }

    public async Task<MemberData> GetMemberDataAsync(Guid accountId, Guid memberId)
    {
        using var response = await SendAsync(HttpMethod.Get, $"accounts/{accountId}/members/{memberId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MemberData>(JsonOptions) ?? new MemberData();
    }

    public async Task SaveMemberDataAsync(Guid accountId, Guid memberId, MemberData data)
    {
        using var response = await SendAsync(HttpMethod.Put, $"accounts/{accountId}/members/{memberId}", data);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteMemberDataAsync(Guid accountId, Guid memberId)
    {
        using var response = await SendAsync(HttpMethod.Delete, $"accounts/{accountId}/members/{memberId}");
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Permanently deletes the account on the server — every member's data,
    /// the shared library, and the account index. Not part of IWorkoutRepository:
    /// this is a direct-to-server action (like DevSettingsViewModel's own direct use
    /// of this class), never something to queue through SyncingWorkoutRepository's
    /// local-first/best-effort model — the caller must know it actually succeeded
    /// before clearing any local state. See ProfileViewModel.DeleteAccount.</summary>
    public async Task DeleteAccountAsync(Guid accountId)
    {
        using var response = await SendAsync(HttpMethod.Delete, $"accounts/{accountId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<ManufacturerLibrary> GetManufacturerLibraryAsync()
    {
        using var response = await SendAsync(HttpMethod.Get, "library/manufacturer");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ManufacturerLibrary>(JsonOptions) ?? new ManufacturerLibrary();
    }

    public async Task SaveManufacturerLibraryAsync(ManufacturerLibrary library)
    {
        using var response = await SendAsync(HttpMethod.Put, "library/manufacturer", library);
        response.EnsureSuccessStatusCode();
    }

    public async Task<RigCatalog> GetRigCatalogAsync()
    {
        using var response = await SendAsync(HttpMethod.Get, "library/rigs");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RigCatalog>(JsonOptions) ?? new RigCatalog();
    }

    public async Task SaveRigCatalogAsync(RigCatalog catalog)
    {
        using var response = await SendAsync(HttpMethod.Put, "library/rigs", catalog);
        response.EnsureSuccessStatusCode();
    }

    public async Task<byte[]?> GetProgressPhotoBlobAsync(Guid accountId, Guid memberId, string blobFileName)
    {
        using var response = await SendBinaryAsync(HttpMethod.Get, $"accounts/{accountId}/members/{memberId}/photos/{blobFileName}", null, null);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync();
    }

    public async Task SaveProgressPhotoBlobAsync(Guid accountId, Guid memberId, string blobFileName, byte[] jpegBytes)
    {
        using var response = await SendBinaryAsync(HttpMethod.Put, $"accounts/{accountId}/members/{memberId}/photos/{blobFileName}", jpegBytes, "image/jpeg");
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteProgressPhotoBlobAsync(Guid accountId, Guid memberId, string blobFileName)
    {
        using var response = await SendBinaryAsync(HttpMethod.Delete, $"accounts/{accountId}/members/{memberId}/photos/{blobFileName}", null, null);
        // Delete is idempotent — a 404 (already gone, or never pushed) is not an error.
        if (response.StatusCode != HttpStatusCode.NotFound) response.EnsureSuccessStatusCode();
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        var apiKey = await RemoteApiConfig.GetApiKeyAsync();
        if (!string.IsNullOrEmpty(apiKey)) request.Headers.Add("X-Api-Key", apiKey);

        // The API key above only proves "this is our app" — every /accounts/** endpoint
        // now separately checks this bearer token's Uid against the account's own linked
        // identities (see Program.cs's AuthorizeAccountAsync) before allowing access.
        // /library/manufacturer doesn't check it, so it's harmless to attach here even
        // before a real sign-in has happened (GetValidIdTokenAsync just returns null then).
        var idToken = await _firebaseAuth.GetValidIdTokenAsync();
        if (!string.IsNullOrEmpty(idToken)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

        if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
        return await _http.SendAsync(request);
    }

    // Binary-body sibling to SendAsync above — a JPEG isn't JSON, so this
    // attaches the same X-Api-Key/Bearer-token pair but with a raw
    // ByteArrayContent body instead of JsonContent.
    private async Task<HttpResponseMessage> SendBinaryAsync(HttpMethod method, string path, byte[]? body, string? contentType)
    {
        var request = new HttpRequestMessage(method, path);
        var apiKey = await RemoteApiConfig.GetApiKeyAsync();
        if (!string.IsNullOrEmpty(apiKey)) request.Headers.Add("X-Api-Key", apiKey);

        var idToken = await _firebaseAuth.GetValidIdTokenAsync();
        if (!string.IsNullOrEmpty(idToken)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

        if (body is not null)
        {
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType!);
        }
        return await _http.SendAsync(request);
    }
}
