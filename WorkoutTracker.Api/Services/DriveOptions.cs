namespace WorkoutTracker.Api.Services;

/// <summary>
/// Credentials for the dedicated storage account (not any end user's own Google
/// account). Bound from configuration section "Drive" — local dev via
/// user-secrets, production via Cloud Run environment variables / Secret Manager.
/// Never logged, never returned from any endpoint, never present in the mobile app.
/// </summary>
public class DriveOptions
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string RefreshToken { get; set; } = "";
}
