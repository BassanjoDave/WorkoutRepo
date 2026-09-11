using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Maui.Authentication;

namespace WorkoutTracker.Services;

public record GoogleAuthResult(string Sub, string? Email, string? Name, string IdToken);

/// <summary>
/// Authenticates the member against their real Google account via the
/// browser (Authorization Code + PKCE, no client secret — Android/iOS OAuth
/// clients are public clients, Google doesn't issue a secret for them).
/// Ties a Member to an Identity by Google's stable "sub" claim. Used both by
/// MemberEditViewModel (link an existing local member to a Google identity)
/// and by ProfileGateViewModel (sign in as the app-entry action, looking the
/// identity up server-side to find or create the account it belongs to —
/// see /identities/lookup and /identities/claim in WorkoutTracker.Api).
/// </summary>
public interface IGoogleAuthService
{
    /// <summary>Returns null if the user cancels or the flow fails — never throws for those cases;
    /// check LastError afterward for why.</summary>
    Task<GoogleAuthResult?> SignInAsync();

    /// <summary>Set right before SignInAsync returns null; null itself means the user simply
    /// cancelled (no error to show). Overwritten on every call, so read it immediately after.</summary>
    string? LastError { get; }
}

public class GoogleAuthService : IGoogleAuthService
{
    // From the Android OAuth client created in Google Cloud Console
    // (workout-tracker-507101 project) for package com.davidsworkoutapp.tracker.
    // Not a secret — Android/iOS client ids are meant to be embedded in the app.
    // NOTE: this is the ANDROID client only. iOS needs its own OAuth client
    // (registered against the iOS bundle id) before sign-in will work there —
    // the iOS platform scaffolding (AppDelegate.OpenUrl, Info.plist scheme) is
    // in place, but pointed at this Android client id purely so everything
    // compiles; swap in a real iOS client id/redirect once one exists.
    private const string ClientId = "400830965498-9sb49vap4o1omq0chp8oqsn6ao8ckt6s.apps.googleusercontent.com";
    private const string RedirectUri = "com.googleusercontent.apps.400830965498-9sb49vap4o1omq0chp8oqsn6ao8ckt6s:/oauth2redirect";

    // A separate "Desktop app" OAuth client (workout-tracker-507101 project) — the
    // Android client above can't be used on Windows. MAUI's WebAuthenticator throws
    // outright on an unpackaged Windows app (WindowsPackageType=None here): its
    // Windows implementation needs Windows.Security.Authentication.Web.WebAuthenticationBroker,
    // which requires a packaged app identity. Desktop app OAuth clients are exactly
    // Google's own answer to that: they support a "loopback IP address" redirect
    // (http://127.0.0.1:<port>/), so Windows runs its own flow below — open the
    // system browser directly and catch the redirect with a local HTTP listener —
    // instead of going through WebAuthenticator at all.
    private const string WindowsClientId = "400830965498-s5eu2t3jbu36lcd3cbgpra8d518nsdk6.apps.googleusercontent.com";

    // Unlike Android/iOS clients, Google's "Desktop app" client type requires this on
    // every token exchange even though PKCE is also in use — the request otherwise
    // fails with "client_secret is missing." Not meaningfully secret once shipped in
    // an app binary (same caveat as the client id above, and Google's own guidance
    // for this client type); it doesn't gate anything a real secret would (the actual
    // trust boundary is still the user completing Google's own consent screen).
    private const string WindowsClientSecret = "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";

    public string? LastError { get; private set; }

    public async Task<GoogleAuthResult?> SignInAsync()
    {
        LastError = null;
#if WINDOWS
        return await SignInWindowsAsync();
#else
        return await SignInWebAuthenticatorAsync();
#endif
    }

#if WINDOWS
    private async Task<GoogleAuthResult?> SignInWindowsAsync()
    {
        var (verifier, challenge) = GeneratePkce();

        HttpListener listener;
        string redirectUri;
        try
        {
            var port = GetFreeLoopbackPort();
            redirectUri = $"http://127.0.0.1:{port}/";
            listener = new HttpListener();
            listener.Prefixes.Add(redirectUri);
            listener.Start();
        }
        catch (Exception ex)
        {
            LastError = $"Couldn't start the local sign-in listener: {ex.Message}";
            return null;
        }

        try
        {
            var authUrl = "https://accounts.google.com/o/oauth2/v2/auth"
                + $"?client_id={Uri.EscapeDataString(WindowsClientId)}"
                + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
                + "&response_type=code"
                + $"&scope={Uri.EscapeDataString("openid email profile")}"
                + $"&code_challenge={challenge}&code_challenge_method=S256"
                + "&prompt=select_account";

            try { await Microsoft.Maui.ApplicationModel.Launcher.Default.OpenAsync(authUrl); }
            catch (Exception ex) { LastError = $"Couldn't open the browser: {ex.Message}"; return null; }

            HttpListenerContext context;
            try
            {
                var getContext = listener.GetContextAsync();
                var completed = await Task.WhenAny(getContext, Task.Delay(TimeSpan.FromMinutes(3)));
                if (completed != getContext) { LastError = "Timed out waiting for Google sign-in to complete."; return null; }
                context = await getContext;
            }
            catch (Exception ex)
            {
                LastError = $"Sign-in listener failed: {ex.Message}";
                return null;
            }

            var code = context.Request.QueryString["code"];
            var error = context.Request.QueryString["error"];
            await RespondToBrowserAsync(context, success: !string.IsNullOrEmpty(code));

            if (!string.IsNullOrEmpty(error))
            {
                LastError = $"Google sign-in was not completed: {error}";
                return null;
            }
            if (string.IsNullOrEmpty(code))
            {
                LastError = "Google's redirect didn't include an authorization code.";
                return null;
            }

            using var http = new HttpClient();
            HttpResponseMessage tokenResponse;
            try
            {
                tokenResponse = await http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["code"] = code,
                    ["client_id"] = WindowsClientId,
                    ["client_secret"] = WindowsClientSecret,
                    ["redirect_uri"] = redirectUri,
                    ["grant_type"] = "authorization_code",
                    ["code_verifier"] = verifier,
                }));
            }
            catch (Exception ex)
            {
                LastError = $"Couldn't reach Google's token endpoint: {ex.Message}";
                return null;
            }
            if (!tokenResponse.IsSuccessStatusCode)
            {
                LastError = $"Google rejected the sign-in ({(int)tokenResponse.StatusCode}): {await tokenResponse.Content.ReadAsStringAsync()}";
                return null;
            }

            var json = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
            if (!json.TryGetProperty("id_token", out var idTokenElement))
            {
                LastError = "Google's token response didn't include an ID token.";
                return null;
            }
            var idToken = idTokenElement.GetString();
            if (string.IsNullOrEmpty(idToken))
            {
                LastError = "Google's token response had an empty ID token.";
                return null;
            }
            var result = DecodeIdToken(idToken);
            if (result is null) LastError = "Couldn't decode the ID token Google returned.";
            return result;
        }
        finally
        {
            listener.Stop();
            listener.Close();
        }
    }

    private static async Task RespondToBrowserAsync(HttpListenerContext context, bool success)
    {
        var message = success
            ? "Signed in - you can close this tab and return to Rig Ritual."
            : "Sign-in was not completed - you can close this tab and return to Rig Ritual.";
        var html = $"<html><body>{message}</body></html>";
        // charset=utf-8 matters here even though this message is plain ASCII: without it browsers
        // guess Latin-1/Windows-1252 for text/html, which garbles anything non-ASCII on a real
        // Google display name later reusing this responder — declaring it now avoids re-learning
        // that the hard way (it's exactly what garbled an em dash this string used to have).
        context.Response.ContentType = "text/html; charset=utf-8";
        var buffer = Encoding.UTF8.GetBytes(html);
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer);
        context.Response.Close();
    }

    /// <summary>Binds a TcpListener to port 0 to get an OS-assigned free port, then releases
    /// it immediately so HttpListener can bind the same port — a small, common race (something
    /// else could grab it in between) that's an acceptable simplification for a local, once-off,
    /// user-initiated sign-in flow.</summary>
    private static int GetFreeLoopbackPort()
    {
        var tcpListener = new TcpListener(IPAddress.Loopback, 0);
        tcpListener.Start();
        var port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
        tcpListener.Stop();
        return port;
    }
#endif

    private async Task<GoogleAuthResult?> SignInWebAuthenticatorAsync()
    {
        var (verifier, challenge) = GeneratePkce();
        var authUrl = "https://accounts.google.com/o/oauth2/v2/auth"
            + $"?client_id={Uri.EscapeDataString(ClientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
            + "&response_type=code"
            + $"&scope={Uri.EscapeDataString("openid email profile")}"
            + $"&code_challenge={challenge}&code_challenge_method=S256"
            + "&prompt=select_account";

        WebAuthenticatorResult authResult;
        try
        {
            authResult = await WebAuthenticator.Default.AuthenticateAsync(new WebAuthenticatorOptions
            {
                Url = new Uri(authUrl),
                CallbackUrl = new Uri(RedirectUri),
            });
        }
        catch (TaskCanceledException)
        {
            return null; // user cancelled — not an error worth reporting
        }
        catch (Exception ex)
        {
            LastError = $"Sign-in failed: {ex.Message}";
            return null;
        }

        if (!authResult.Properties.TryGetValue("code", out var code) || string.IsNullOrEmpty(code))
        {
            LastError = "Google's redirect didn't include an authorization code.";
            return null;
        }

        using var http = new HttpClient();
        HttpResponseMessage tokenResponse;
        try
        {
            tokenResponse = await http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = ClientId,
                ["redirect_uri"] = RedirectUri,
                ["grant_type"] = "authorization_code",
                ["code_verifier"] = verifier,
            }));
        }
        catch (Exception ex)
        {
            LastError = $"Couldn't reach Google's token endpoint: {ex.Message}";
            return null;
        }
        if (!tokenResponse.IsSuccessStatusCode)
        {
            LastError = $"Google rejected the sign-in ({(int)tokenResponse.StatusCode}): {await tokenResponse.Content.ReadAsStringAsync()}";
            return null;
        }

        var json = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        if (!json.TryGetProperty("id_token", out var idTokenElement))
        {
            LastError = "Google's token response didn't include an ID token.";
            return null;
        }
        var idToken = idTokenElement.GetString();
        if (string.IsNullOrEmpty(idToken))
        {
            LastError = "Google's token response had an empty ID token.";
            return null;
        }
        var result = DecodeIdToken(idToken);
        if (result is null) LastError = "Couldn't decode the ID token Google returned.";
        return result;
    }

    private static (string Verifier, string Challenge) GeneratePkce()
    {
        var verifier = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return (verifier, challenge);
    }

    /// <summary>
    /// Decodes the ID token's claims client-side without verifying its
    /// cryptographic signature — fine for reading Sub/Email/Name locally, since
    /// the token can only have reached this code via Google's real consent
    /// screen redirecting into this exact signed build. The raw IdToken is
    /// carried along on the result too, though: anything that sends it on to
    /// the server (identity lookup/claim) needs the server to independently
    /// verify its signature first, since at that point it's authorizing access
    /// to cross-device data, not just a local decode.
    /// </summary>
    private static GoogleAuthResult? DecodeIdToken(string idToken)
    {
        var parts = idToken.Split('.');
        if (parts.Length < 2) return null;

        var payload = JsonSerializer.Deserialize<JsonElement>(Base64UrlDecode(parts[1]));
        if (!payload.TryGetProperty("sub", out var subElement)) return null;
        var sub = subElement.GetString();
        if (string.IsNullOrEmpty(sub)) return null;

        var email = payload.TryGetProperty("email", out var e) ? e.GetString() : null;
        var name = payload.TryGetProperty("name", out var n) ? n.GetString() : null;
        return new GoogleAuthResult(sub, email, name, idToken);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };
        return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }
}
