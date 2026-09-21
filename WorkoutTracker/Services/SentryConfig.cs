namespace WorkoutTracker.Services;

/// <summary>
/// A Sentry DSN is a write-only, publicly-shippable project identifier (not a
/// secret like RemoteApiConfig.ApiKey — Sentry's own docs embed it directly in
/// client source), so unlike the API key this is a plain constant, not
/// SecureStorage. Passing an empty string is Sentry's own documented way to
/// disable the SDK safely (passing null throws) — see
/// https://github.com/getsentry/sentry-dotnet/issues/3136 — so this app runs
/// with crash reporting off until a real DSN is filled in here.
///
/// Fill in once you've created a free project at sentry.io (Platform: .NET
/// MAUI) — see the monetization/publish-prep plan's crash-reporting section.
/// </summary>
public static class SentryConfig
{
    public const string Dsn = "";
}
