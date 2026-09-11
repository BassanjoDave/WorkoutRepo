using System.Net.Http.Json;

namespace WorkoutTracker.Services.Storage;

/// <summary>
/// Talks to the API's /identities endpoints — the server-side directory that
/// maps a Google identity to the Account(s) it's linked to, making "sign in on
/// a new device and find my data" possible. See IdentityDirectory.cs and
/// Program.cs's /identities/lookup and /identities/claim for the server side.
/// </summary>
public interface IIdentityService
{
    /// <summary>Accounts already linked to this Google identity, or an empty list if none.</summary>
    Task<List<Guid>> LookupAsync(string idToken);

    /// <summary>Links this Google identity to accountId in the server-side directory (idempotent — adding an already-linked account is a no-op).</summary>
    Task ClaimAsync(string idToken, Guid accountId);
}

public class IdentityService : IIdentityService
{
    private readonly HttpClient _http;

    public IdentityService(HttpClient http)
    {
        _http = http;
        _http.BaseAddress = new Uri(RemoteApiConfig.BaseUrl);
    }

    public async Task<List<Guid>> LookupAsync(string idToken)
    {
        using var response = await SendAsync("identities/lookup", new { IdToken = idToken });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<LookupResponse>();
        return result?.AccountIds ?? new List<Guid>();
    }

    public async Task ClaimAsync(string idToken, Guid accountId)
    {
        using var response = await SendAsync("identities/claim", new { IdToken = idToken, AccountId = accountId });
        response.EnsureSuccessStatusCode();
    }

    private async Task<HttpResponseMessage> SendAsync(string path, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        var apiKey = await RemoteApiConfig.GetApiKeyAsync();
        if (!string.IsNullOrEmpty(apiKey)) request.Headers.Add("X-Api-Key", apiKey);
        return await _http.SendAsync(request);
    }

    private record LookupResponse(List<Guid> AccountIds);
}
