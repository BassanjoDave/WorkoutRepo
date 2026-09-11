using Android.App;
using Android.Content;
using Android.Content.PM;

namespace WorkoutTracker;

/// <summary>
/// Catches the browser's redirect back into the app after Google's consent
/// screen. The DataScheme here is the reversed form of the Android OAuth
/// client id registered in Google Cloud Console (see GoogleAuthService) —
/// Google's own convention for this client type, so this must stay in sync
/// with GoogleAuthService.RedirectUri if the OAuth client is ever recreated.
/// </summary>
[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "com.googleusercontent.apps.400830965498-9sb49vap4o1omq0chp8oqsn6ao8ckt6s")]
public class WebAuthenticationCallbackActivity : Microsoft.Maui.Authentication.WebAuthenticatorCallbackActivity
{
}
