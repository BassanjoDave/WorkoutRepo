using System.Text.Json;
using System.Text.Json.Serialization;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Microsoft.Extensions.Options;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace WorkoutTracker.Api.Services;

public class DriveDocumentStore : IDriveDocumentStore
{
    private readonly DriveService _drive;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public DriveDocumentStore(IOptions<DriveOptions> options)
    {
        var o = options.Value;
        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets { ClientId = o.ClientId, ClientSecret = o.ClientSecret },
            Scopes = new[] { DriveService.Scope.DriveAppdata },
        });
        // UserCredential handles refreshing the access token from this refresh
        // token automatically on every call — no manual token refresh logic needed.
        var credential = new UserCredential(flow, "workout-tracker-storage-account", new TokenResponse { RefreshToken = o.RefreshToken });

        _drive = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "WorkoutTracker API",
        });
    }

    public async Task<T?> GetAsync<T>(string name) where T : class
    {
        var fileId = await FindFileIdAsync(name);
        if (fileId is null) return null;

        using var stream = new MemoryStream();
        await _drive.Files.Get(fileId).DownloadAsync(stream);
        stream.Position = 0;
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions);
    }

    public async Task SaveAsync<T>(string name, T value)
    {
        using var stream = new MemoryStream();
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions);
        stream.Position = 0;

        var existingId = await FindFileIdAsync(name);
        if (existingId is null)
        {
            var metadata = new DriveFile { Name = name, Parents = new List<string> { "appDataFolder" } };
            var request = _drive.Files.Create(metadata, stream, "application/json");
            await request.UploadAsync();
        }
        else
        {
            var request = _drive.Files.Update(new DriveFile(), existingId, stream, "application/json");
            await request.UploadAsync();
        }
    }

    public async Task DeleteAsync(string name)
    {
        var fileId = await FindFileIdAsync(name);
        if (fileId is not null) await _drive.Files.Delete(fileId).ExecuteAsync();
    }

    public async Task<byte[]?> GetBlobAsync(string name)
    {
        var fileId = await FindFileIdAsync(name);
        if (fileId is null) return null;

        using var stream = new MemoryStream();
        await _drive.Files.Get(fileId).DownloadAsync(stream);
        return stream.ToArray();
    }

    public async Task SaveBlobAsync(string name, byte[] bytes, string contentType)
    {
        using var stream = new MemoryStream(bytes);
        var existingId = await FindFileIdAsync(name);
        if (existingId is null)
        {
            var metadata = new DriveFile { Name = name, Parents = new List<string> { "appDataFolder" } };
            await _drive.Files.Create(metadata, stream, contentType).UploadAsync();
        }
        else
        {
            await _drive.Files.Update(new DriveFile(), existingId, stream, contentType).UploadAsync();
        }
    }

    public Task DeleteBlobAsync(string name) => DeleteAsync(name);

    private async Task<string?> FindFileIdAsync(string name)
    {
        var request = _drive.Files.List();
        request.Spaces = "appDataFolder";
        request.Q = $"name = '{EscapeForQuery(name)}' and trashed = false";
        request.Fields = "files(id)";
        var result = await request.ExecuteAsync();
        return result.Files.FirstOrDefault()?.Id;
    }

    private static string EscapeForQuery(string name) => name.Replace("'", "\\'");
}
