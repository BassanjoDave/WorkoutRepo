using System.Net.Http.Json;
using System.Text.Json;

namespace WorkoutTracker.Services.Storage;

public record FirebaseAuthResult(string IdToken, string Uid, string? Email, string? DisplayName, string RefreshToken, int ExpiresInSeconds);

/// <summary>
/// Talks to Firebase's Identity Toolkit REST API directly (no native Firebase SDK —
/// there isn't one for Windows, and the REST API works identically on every platform,
/// matching the same reasoning as GoogleAuthService's Windows loopback flow). Every
/// method here returns the same shape of result regardless of how the user actually
/// signed in, because that's the point of switching to Firebase: the server only ever
/// needs to verify one kind of ID token (see WorkoutTracker.Api's VerifyFirebaseIdToken),
/// not a different one per sign-in method.
/// </summary>
public interface IFirebaseAuthService
{
    /// <summary>Exchanges an already-obtained Google ID token (from IGoogleAuthService)
    /// for a Firebase session — Google sign-in still goes through Google's own OAuth
    /// dance first; this is the one extra step that unifies it with the other methods.</summary>
    Task<FirebaseAuthResult?> SignInWithGoogleAsync(string googleIdToken);

    Task<FirebaseAuthResult?> SignInWithEmailAsync(string email, string password);

    /// <summary>Fails if the email is already registered — Firebase reports that as
    /// EMAIL_EXISTS in LastError, which the caller should offer as "sign in instead."</summary>
    Task<FirebaseAuthResult?> SignUpWithEmailAsync(string email, string password);

    /// <summary>Sends a password-reset email via Firebase for the given address.
    /// Returns true whether or not that email is actually registered — Firebase's
    /// own response doesn't distinguish (deliberately, to avoid leaking which emails
    /// have accounts), so the caller should show the same "check your email" message
    /// either way rather than treating a false return as "no such account."</summary>
    Task<bool> SendPasswordResetAsync(string email);

    /// <summary>Sends a verification email to the address behind the given (already
    /// signed-in) Firebase ID token.</summary>
    Task<bool> SendEmailVerificationAsync(string idToken);

    /// <summary>Resumes a still-valid stored session (refreshing the ID token first if
    /// needed) without any new sign-in prompt — the biometric-unlock path uses this:
    /// Face ID/Touch ID/Windows Hello confirms it's you, then this stands in for a
    /// fresh Firebase sign-in using the session already on the device. Returns null if
    /// there's no stored session, same as GetValidIdTokenAsync.</summary>
    Task<FirebaseAuthResult?> ResumeSessionAsync();

    /// <summary>A currently-valid Firebase ID token for authenticating requests to our
    /// own API — every account-scoped endpoint now checks this token's Uid against the
    /// account's linked identities (see Program.cs's AuthorizeAccountAsync), replacing
    /// the old shared API key as the thing that actually proves "this is you." ID tokens
    /// only last an hour, so this transparently refreshes via the stored refresh token
    /// when the last one is expired or close to it. Returns null if there's no stored
    /// session (never signed in this launch, since sign-in is required every launch).</summary>
    Task<string?> GetValidIdTokenAsync();

    /// <summary>Set right before any method returns null; read it immediately after.</summary>
    string? LastError { get; }
}

public class FirebaseAuthService : IFirebaseAuthService
{
    // Firebase's "Web API Key" — despite the name, this is the correct key for every
    // platform's REST calls (there's no separate Windows/Android/iOS key the way OAuth
    // client ids work). Not a secret: Firebase's own docs are explicit that this key
    // identifies the project, not a caller, and is expected to ship inside client apps.
    private const string WebApiKey = "AIzaSyA58hP36NoBnB47hvbYQmVerz1lsHJWicU";
    private const string BaseUrl = "https://identitytoolkit.googleapis.com/v1/";
    private const string RefreshUrl = "https://securetoken.googleapis.com/v1/token";

    private const string IdTokenKey = "firebase_id_token";
    private const string RefreshTokenKey = "firebase_refresh_token";
    private const string ExpiresAtKey = "firebase_token_expires_at";

    private readonly HttpClient _http = new();

    public string? LastError { get; private set; }

    public Task<FirebaseAuthResult?> SignInWithGoogleAsync(string googleIdToken) => PostAsync("accounts:signInWithIdp", new
    {
        postBody = $"id_token={googleIdToken}&providerId=google.com",
        requestUri = "http://localhost",
        returnIdpCredential = true,
        returnSecureToken = true,
    });

    public Task<FirebaseAuthResult?> SignInWithEmailAsync(string email, string password) =>
        PostAsync("accounts:signInWithPassword", new { email, password, returnSecureToken = true });

    public Task<FirebaseAuthResult?> SignUpWithEmailAsync(string email, string password) =>
        PostAsync("accounts:signUp", new { email, password, returnSecureToken = true });

    public async Task<bool> SendPasswordResetAsync(string email)
    {
        LastError = null;
        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync($"{BaseUrl}accounts:sendOobCode?key={WebApiKey}",
                new { requestType = "PASSWORD_RESET", email });
        }
        catch (Exception ex)
        {
            LastError = $"Couldn't reach Firebase: {ex.Message}";
            return false;
        }

        if (response.IsSuccessStatusCode) return true;
        LastError = ExtractErrorMessage(await response.Content.ReadAsStringAsync());
        return false;
    }

    public async Task<bool> SendEmailVerificationAsync(string idToken)
    {
        LastError = null;
        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync($"{BaseUrl}accounts:sendOobCode?key={WebApiKey}",
                new { requestType = "VERIFY_EMAIL", idToken });
        }
        catch (Exception ex)
        {
            LastError = $"Couldn't reach Firebase: {ex.Message}";
            return false;
        }

        if (response.IsSuccessStatusCode) return true;
        LastError = ExtractErrorMessage(await response.Content.ReadAsStringAsync());
        return false;
    }

    public async Task<FirebaseAuthResult?> ResumeSessionAsync()
    {
        var idToken = await GetValidIdTokenAsync();
        var refreshToken = await SecureStorage.Default.GetAsync(RefreshTokenKey);
        if (idToken is null || string.IsNullOrEmpty(refreshToken)) return null;

        var (uid, email) = DecodeIdTokenClaims(idToken);
        if (uid is null) return null;
        return new FirebaseAuthResult(idToken, uid, email, null, refreshToken, 0);
    }

    /// <summary>Reads Uid/Email out of the token's own claims without verifying its
    /// signature — fine here since it never left this device's own SecureStorage
    /// (mirrors GoogleAuthService.DecodeIdToken's identical reasoning and shape).
    /// Anything sent on to the server still gets independently signature-verified there.</summary>
    private static (string? Uid, string? Email) DecodeIdTokenClaims(string idToken)
    {
        var parts = idToken.Split('.');
        if (parts.Length < 2) return (null, null);
        try
        {
            var payload = JsonSerializer.Deserialize<JsonElement>(Base64UrlDecode(parts[1]));
            var uid = payload.TryGetProperty("sub", out var s) ? s.GetString() : null;
            var email = payload.TryGetProperty("email", out var e) ? e.GetString() : null;
            return (uid, email);
        }
        catch (Exception)
        {
            return (null, null);
        }
    }

    private static string Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };
        return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }

    public async Task<string?> GetValidIdTokenAsync()
    {
        var refreshToken = await SecureStorage.Default.GetAsync(RefreshTokenKey);
        if (string.IsNullOrEmpty(refreshToken)) return null;

        var idToken = await SecureStorage.Default.GetAsync(IdTokenKey);
        var expiresAtRaw = await SecureStorage.Default.GetAsync(ExpiresAtKey);
        if (!string.IsNullOrEmpty(idToken) && DateTimeOffset.TryParse(expiresAtRaw, out var expiresAt)
            && expiresAt > DateTimeOffset.UtcNow.AddMinutes(2))
        {
            return idToken;
        }

        var refreshed = await RefreshAsync(refreshToken);
        return refreshed?.IdToken;
    }

    private async Task<FirebaseAuthResult?> RefreshAsync(string refreshToken)
    {
        LastError = null;
        HttpResponseMessage response;
        try
        {
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
            });
            response = await _http.PostAsync($"{RefreshUrl}?key={WebApiKey}", form);
        }
        catch (Exception ex)
        {
            LastError = $"Couldn't reach Firebase: {ex.Message}";
            return null;
        }

        var raw = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            LastError = ExtractErrorMessage(raw);
            // A refresh token that's been revoked (password changed elsewhere, account
            // disabled) will never succeed again — clear it so every subsequent call
            // fails fast as "not signed in" instead of retrying a dead token forever.
            await ClearSessionAsync();
            return null;
        }

        JsonElement json;
        try { json = JsonSerializer.Deserialize<JsonElement>(raw); }
        catch (JsonException) { LastError = "Firebase returned a response that couldn't be parsed."; return null; }

        // The refresh endpoint uses snake_case fields, unlike Identity Toolkit's own
        // sign-in endpoints below — a genuine Google API inconsistency, not a typo.
        var idToken = json.TryGetProperty("id_token", out var t) ? t.GetString() : null;
        var newRefreshToken = json.TryGetProperty("refresh_token", out var r) ? r.GetString() : null;
        var uid = json.TryGetProperty("user_id", out var u) ? u.GetString() : null;
        var expiresInRaw = json.TryGetProperty("expires_in", out var ei) ? ei.GetString() : null;
        if (string.IsNullOrEmpty(idToken) || string.IsNullOrEmpty(newRefreshToken) || string.IsNullOrEmpty(uid))
        {
            LastError = "Firebase's refresh response didn't include the expected fields.";
            return null;
        }
        var expiresIn = int.TryParse(expiresInRaw, out var secs) ? secs : 3600;

        await PersistSessionAsync(idToken, newRefreshToken, expiresIn);
        return new FirebaseAuthResult(idToken, uid, null, null, newRefreshToken, expiresIn);
    }

    private async Task<FirebaseAuthResult?> PostAsync(string method, object body)
    {
        LastError = null;
        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync($"{BaseUrl}{method}?key={WebApiKey}", body);
        }
        catch (Exception ex)
        {
            LastError = $"Couldn't reach Firebase: {ex.Message}";
            return null;
        }

        var raw = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            LastError = ExtractErrorMessage(raw);
            return null;
        }

        JsonElement json;
        try { json = JsonSerializer.Deserialize<JsonElement>(raw); }
        catch (JsonException) { LastError = "Firebase returned a response that couldn't be parsed."; return null; }

        var idToken = json.TryGetProperty("idToken", out var t) ? t.GetString() : null;
        var uid = json.TryGetProperty("localId", out var l) ? l.GetString() : null;
        var refreshToken = json.TryGetProperty("refreshToken", out var rt) ? rt.GetString() : null;
        if (string.IsNullOrEmpty(idToken) || string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(refreshToken))
        {
            LastError = "Firebase's response didn't include the expected sign-in fields.";
            return null;
        }

        var email = json.TryGetProperty("email", out var e) ? e.GetString() : null;
        var displayName = json.TryGetProperty("displayName", out var d) ? d.GetString() : null;
        var expiresInRaw = json.TryGetProperty("expiresIn", out var ei) ? ei.GetString() : null;
        var expiresIn = int.TryParse(expiresInRaw, out var secs) ? secs : 3600;

        await PersistSessionAsync(idToken, refreshToken, expiresIn);
        return new FirebaseAuthResult(idToken, uid, email, displayName, refreshToken, expiresIn);
    }

    private static async Task PersistSessionAsync(string idToken, string refreshToken, int expiresInSeconds)
    {
        await SecureStorage.Default.SetAsync(IdTokenKey, idToken);
        await SecureStorage.Default.SetAsync(RefreshTokenKey, refreshToken);
        await SecureStorage.Default.SetAsync(ExpiresAtKey, DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds).ToString("O"));
    }

    private static Task ClearSessionAsync()
    {
        SecureStorage.Default.Remove(IdTokenKey);
        SecureStorage.Default.Remove(RefreshTokenKey);
        SecureStorage.Default.Remove(ExpiresAtKey);
        return Task.CompletedTask;
    }

    /// <summary>Firebase's error shape is {"error":{"message":"EMAIL_EXISTS", ...}} —
    /// the message is a machine-readable code, not prose, but it's specific enough to
    /// show directly (e.g. "EMAIL_EXISTS", "INVALID_PASSWORD", "WEAK_PASSWORD").</summary>
    private static string ExtractErrorMessage(string raw)
    {
        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(raw);
            if (json.TryGetProperty("error", out var error) && error.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? raw;
            }
        }
        catch (JsonException) { /* fall through to raw text below */ }
        return raw;
    }
}
