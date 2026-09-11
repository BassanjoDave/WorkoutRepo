using SkiaSharp;

namespace WorkoutTracker.Services;

/// <summary>
/// Captures/picks a progress photo and returns it already resized (~1200px
/// longest edge) and JPEG-compressed (~80% quality) — see the locked
/// compression target in the Progress Photos plan. No platform-conditional
/// code needed here: MediaPicker and SkiaSharp both behave identically on
/// every target, unlike IBiometricAuthService's per-OS APIs.
/// </summary>
public interface IProgressPhotoCaptureService
{
    /// <summary>Shows the Take Photo / Choose from Library action sheet, then
    /// resizes+compresses the result. Returns null if the user cancels, the
    /// platform doesn't support the chosen option, or permission is denied —
    /// caller shows its own message (mirrors ExerciseEditorViewModel.AddPhoto's
    /// exception handling, reused verbatim here).</summary>
    Task<byte[]?> CaptureAsync();
}

public class ProgressPhotoCaptureService : IProgressPhotoCaptureService
{
    private const int MaxLongestEdge = 1200;
    private const int JpegQuality = 80;

    public async Task<byte[]?> CaptureAsync()
    {
        var page = Shell.Current?.CurrentPage;
        if (page is null) return null;

        var choice = await page.DisplayActionSheetAsync("Add Progress Photo", "Cancel", null, "Take Photo", "Choose from Library");
        if (choice is null || choice == "Cancel") return null;

        FileResult? result;
        try
        {
            if (choice == "Take Photo")
            {
                result = await MediaPicker.Default.CapturePhotoAsync();
            }
            else
            {
                // Only ever adding one photo at a time here (unlike
                // ExerciseEditorViewModel.AddPhoto's multi-select) — take just
                // the first pick if the platform's library sheet still allows
                // selecting more than one.
                var picked = await MediaPicker.Default.PickPhotosAsync();
                result = picked.FirstOrDefault();
            }
        }
        catch (FeatureNotSupportedException)
        {
            await page.DisplayAlertAsync("Not supported", "This device doesn't support that option.", "OK");
            return null;
        }
        catch (PermissionException)
        {
            await page.DisplayAlertAsync("Permission needed", "Camera/Photos permission is required to add a progress photo.", "OK");
            return null;
        }
        if (result is null) return null;

        await using var sourceStream = await result.OpenReadAsync();
        using var original = SKBitmap.Decode(sourceStream);
        if (original is null) return null;

        using var resized = ResizeToLongestEdge(original, MaxLongestEdge);
        using var image = SKImage.FromBitmap(resized);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
        return encoded.ToArray();
    }

    private static SKBitmap ResizeToLongestEdge(SKBitmap source, int maxLongestEdge)
    {
        var longestEdge = Math.Max(source.Width, source.Height);
        if (longestEdge <= maxLongestEdge) return source.Copy();

        var scale = (double)maxLongestEdge / longestEdge;
        var newWidth = (int)Math.Round(source.Width * scale);
        var newHeight = (int)Math.Round(source.Height * scale);
        return source.Resize(new SKImageInfo(newWidth, newHeight), SKSamplingOptions.Default) ?? source.Copy();
    }
}
