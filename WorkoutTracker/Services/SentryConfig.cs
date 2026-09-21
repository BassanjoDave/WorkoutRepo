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
/// Sentry project: Rig Ritual (sentry.io), Error Monitoring only — no
/// tracing/logging/metrics enabled there, matching what this app's Sentry.Maui
/// integration actually sends (see MauiProgram.cs's UseSentry call).
/// </summary>
public static class SentryConfig
{
    public const string Dsn = "https://0c6d607801fbf1677da0a5fefe515a3b@o4512126349803520.ingest.us.sentry.io/4512126365794304";
}
