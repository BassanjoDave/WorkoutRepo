using Foundation;
using Microsoft.Maui.Authentication;
using UIKit;

namespace WorkoutTracker;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	// Routes Google's redirect back to WebAuthenticator after the user finishes
	// the consent screen — see GoogleAuthService and Info.plist's CFBundleURLTypes.
	public override bool OpenUrl(UIApplication app, NSUrl url, NSDictionary options)
	{
		if (WebAuthenticator.Default.OpenUrl(url)) return true;
		return base.OpenUrl(app, url, options);
	}
}
