namespace WorkoutTracker.Services.Storage;

/// <summary>
/// Points the client at the deployed thin API. The API key is the same
/// TEMPORARY shared-secret gate documented in WorkoutTracker.Api/Program.cs —
/// it proves the client/server plumbing works, but does not scope access to
/// one account, and gets replaced once Google Sign-In + per-user ID token
/// verification is wired up on both ends.
///
/// The key itself is never written to a file in this project (which lives in
/// a OneDrive-synced folder — exactly the "secret sitting somewhere it can
/// leak" problem the Drive credential was designed to avoid). It lives only
/// in the OS-backed SecureStorage (Keychain / Keystore / DPAPI), seeded once
/// per device via SetApiKeyAsync.
/// </summary>
public static class RemoteApiConfig
{
    public const string BaseUrl = "https://workout-tracker-api-400830965498.us-central1.run.app/";
    private const string ApiKeySecureStorageKey = "remote_api_key";

    public static Task<string?> GetApiKeyAsync() => SecureStorage.Default.GetAsync(ApiKeySecureStorageKey);
    public static Task SetApiKeyAsync(string apiKey) => SecureStorage.Default.SetAsync(ApiKeySecureStorageKey, apiKey);
}
