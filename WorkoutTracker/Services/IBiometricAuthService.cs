#if ANDROID
using AndroidX.Biometric;
using AndroidX.Core.Content;
using AndroidX.Fragment.App;
using Microsoft.Maui.ApplicationModel;
#elif IOS || MACCATALYST
using LocalAuthentication;
using Foundation;
#elif WINDOWS
using Windows.Security.Credentials.UI;
#endif

namespace WorkoutTracker.Services;

/// <summary>
/// Face ID / Touch ID / Windows Hello / Android biometric prompt, used only to
/// re-confirm "it's really you" before resuming a session already stored on this
/// device (see IFirebaseAuthService.ResumeSessionAsync and ProfileGateViewModel's
/// biometric-unlock path) — never a replacement for the underlying Firebase
/// session itself, and never used to skip signing in for the first time on a
/// device. No native SDK setup beyond each platform's own OS-level API: Windows'
/// UserConsentVerifier, iOS/Mac's LocalAuthentication, Android's AndroidX Biometric
/// library — none need extra registration the way Firebase's native SDKs would.
/// </summary>
public interface IBiometricAuthService
{
    bool IsSupported { get; }
    Task<bool> IsAvailableAsync();
    Task<bool> AuthenticateAsync(string reason);
}

public class BiometricAuthService : IBiometricAuthService
{
#if ANDROID
    public bool IsSupported => true;

    public Task<bool> IsAvailableAsync()
    {
        var activity = Platform.CurrentActivity;
        if (activity is null) return Task.FromResult(false);
        var manager = BiometricManager.From(activity);
        var can = manager.CanAuthenticate(BiometricManager.Authenticators.BiometricWeak);
        return Task.FromResult(can == BiometricManager.BiometricSuccess);
    }

    public Task<bool> AuthenticateAsync(string reason)
    {
        var tcs = new TaskCompletionSource<bool>();
        if (Platform.CurrentActivity is not FragmentActivity activity)
        {
            tcs.SetResult(false);
            return tcs.Task;
        }

        var executor = ContextCompat.GetMainExecutor(activity);
        if (executor is null)
        {
            tcs.SetResult(false);
            return tcs.Task;
        }
        var callback = new AuthCallback(tcs);
        var prompt = new BiometricPrompt(activity, executor, callback);
        var promptInfo = new BiometricPrompt.PromptInfo.Builder()
            .SetTitle("Sign in")
            .SetSubtitle(reason)
            .SetNegativeButtonText("Use password instead")
            .Build();
        activity.RunOnUiThread(() => prompt.Authenticate(promptInfo));
        return tcs.Task;
    }

    private class AuthCallback : BiometricPrompt.AuthenticationCallback
    {
        private readonly TaskCompletionSource<bool> _tcs;
        public AuthCallback(TaskCompletionSource<bool> tcs) => _tcs = tcs;
        public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult result) => _tcs.TrySetResult(true);
        public override void OnAuthenticationError(int errorCode, Java.Lang.ICharSequence errString) => _tcs.TrySetResult(false);
        // A single failed scan (bad fingerprint read, etc.) — the system prompt keeps
        // itself open and lets the user retry, so this deliberately doesn't resolve the task.
        public override void OnAuthenticationFailed() { }
    }
#elif IOS || MACCATALYST
    public bool IsSupported => true;

    public Task<bool> IsAvailableAsync()
    {
        using var context = new LAContext();
        return Task.FromResult(context.CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthenticationWithBiometrics, out _));
    }

    public Task<bool> AuthenticateAsync(string reason)
    {
        var context = new LAContext();
        if (!context.CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthenticationWithBiometrics, out _))
            return Task.FromResult(false);

        var tcs = new TaskCompletionSource<bool>();
        context.EvaluatePolicy(LAPolicy.DeviceOwnerAuthenticationWithBiometrics, reason,
            (success, error) => tcs.TrySetResult(success));
        return tcs.Task;
    }
#elif WINDOWS
    public bool IsSupported => true;

    public async Task<bool> IsAvailableAsync()
    {
        try { return await UserConsentVerifier.CheckAvailabilityAsync() == UserConsentVerifierAvailability.Available; }
        catch { return false; }
    }

    public async Task<bool> AuthenticateAsync(string reason)
    {
        try
        {
            var result = await UserConsentVerifier.RequestVerificationAsync(reason);
            return result == UserConsentVerificationResult.Verified;
        }
        catch { return false; }
    }
#else
    public bool IsSupported => false;
    public Task<bool> IsAvailableAsync() => Task.FromResult(false);
    public Task<bool> AuthenticateAsync(string reason) => Task.FromResult(false);
#endif
}
