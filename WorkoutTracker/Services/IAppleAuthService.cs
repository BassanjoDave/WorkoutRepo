#if IOS || MACCATALYST
using AuthenticationServices;
using Foundation;
#endif

namespace WorkoutTracker.Services;

public record AppleAuthResult(string Sub, string? Email, string? Name, string IdToken);

/// <summary>
/// Sign in with Apple — iOS/Mac Catalyst only. Apple's own App Store review
/// guideline (4.8) requires offering this wherever a third-party sign-in like
/// Google is offered, but only on Apple's platforms; Android/Windows have no
/// native equivalent and aren't required to have one, so IsSupported gates the
/// button off there entirely rather than attempting a web-based substitute
/// (mirrors IBiometricAuthService.IsSupported's identical role).
///
/// Native (ASAuthorizationAppleIdProvider) rather than a browser OAuth dance
/// like IGoogleAuthService — Apple's own sign-in credential comes back
/// directly from the OS, no redirect/PKCE plumbing needed. The identity token
/// this returns exchanges for a Firebase session via
/// IFirebaseAuthService.SignInWithAppleAsync, same shape as Google's flow.
///
/// Compiles clean on net10.0-ios and net10.0-maccatalyst (verified via
/// `dotnet build -f net10.0-ios`/`-f net10.0-maccatalyst`, which cross-compiles
/// the managed code fine from Windows without a paired Mac) — but that only
/// proves it builds, not that it runs correctly. Nobody has run the actual
/// sign-in flow on a real device/simulator yet, which needs a Mac; do that
/// before shipping.
/// </summary>
public interface IAppleAuthService
{
    bool IsSupported { get; }

    /// <summary>Returns null if the user cancels or the flow fails — never throws for
    /// those cases; check LastError afterward for why.</summary>
    Task<AppleAuthResult?> SignInAsync();

    /// <summary>Set right before SignInAsync returns null; null itself means the user
    /// simply cancelled (no error to show). Overwritten on every call, so read it
    /// immediately after.</summary>
    string? LastError { get; }
}

#if IOS || MACCATALYST
public class AppleAuthService : IAppleAuthService
{
    public bool IsSupported => true;
    public string? LastError { get; private set; }

    public async Task<AppleAuthResult?> SignInAsync()
    {
        LastError = null;
        try
        {
            var provider = new ASAuthorizationAppleIdProvider();
            var request = provider.CreateRequest();
            request.RequestedScopes = new[] { ASAuthorizationScope.Email, ASAuthorizationScope.FullName };

            var controller = new ASAuthorizationController(new[] { request });
            var del = new AppleSignInDelegate();
            controller.Delegate = del;
            controller.PresentationContextProvider = del;
            controller.PerformRequests();

            var authorization = await del.Task;
            if (authorization is null)
            {
                LastError = del.Error; // null means the user simply cancelled
                return null;
            }

            if (authorization.GetCredential<ASAuthorizationAppleIdCredential>() is not { } credential
                || credential.IdentityToken is null)
            {
                LastError = "Apple's response didn't include an identity token.";
                return null;
            }

            var idToken = new NSString(credential.IdentityToken, NSStringEncoding.UTF8).ToString();
            if (string.IsNullOrEmpty(idToken))
            {
                LastError = "Couldn't decode Apple's identity token.";
                return null;
            }

            // Apple only ever sends the person's name on the very first authorization
            // for this app — every sign-in after that omits it, so this is best-effort,
            // matching how GoogleAuthResult.Name is likewise "whatever the provider
            // happened to hand back this time," not a guaranteed value. Built directly
            // from the given/family name parts rather than NSPersonNameComponentsFormatter
            // (locale-aware ordering isn't worth the extra API surface for a value that's
            // only ever used as a display-name seed for a brand-new local member).
            var name = credential.FullName is { GivenName: not null } or { FamilyName: not null }
                ? $"{credential.FullName?.GivenName} {credential.FullName?.FamilyName}".Trim()
                : null;

            return new AppleAuthResult(credential.User, credential.Email, string.IsNullOrWhiteSpace(name) ? null : name, idToken);
        }
        catch (Exception ex)
        {
            LastError = $"Sign-in failed: {ex.Message}";
            return null;
        }
    }

    /// <summary>Bridges ASAuthorizationController's callback-based API to Task-based
    /// async/await, and supplies the window to present the system sign-in sheet from.</summary>
    private class AppleSignInDelegate : NSObject, IASAuthorizationControllerDelegate, IASAuthorizationControllerPresentationContextProviding
    {
        private readonly TaskCompletionSource<ASAuthorization?> _tcs = new();
        public Task<ASAuthorization?> Task => _tcs.Task;
        public string? Error { get; private set; }

        [Export("authorizationController:didCompleteWithAuthorization:")]
        public void DidComplete(ASAuthorizationController controller, ASAuthorization authorization) =>
            _tcs.TrySetResult(authorization);

        [Export("authorizationController:didCompleteWithError:")]
        public void DidComplete(ASAuthorizationController controller, NSError error)
        {
            // Apple's own "user cancelled" code — leave Error null for that case so the
            // caller treats it the same as any other silent cancel, not a real failure.
            if (error.Code != (nint)ASAuthorizationError.Canceled) Error = error.LocalizedDescription;
            _tcs.TrySetResult(null);
        }

        [Export("presentationAnchorForAuthorizationController:")]
        public UIKit.UIWindow GetPresentationAnchor(ASAuthorizationController controller) =>
            UIKit.UIApplication.SharedApplication.ConnectedScenes
                .OfType<UIKit.UIWindowScene>()
                .SelectMany(scene => scene.Windows)
                .FirstOrDefault(w => w.IsKeyWindow)
            ?? throw new InvalidOperationException("No key window to present Sign in with Apple from.");
    }
}
#else
public class AppleAuthService : IAppleAuthService
{
    public bool IsSupported => false;
    public string? LastError { get; private set; }
    public Task<AppleAuthResult?> SignInAsync() => Task.FromResult<AppleAuthResult?>(null);
}
#endif
