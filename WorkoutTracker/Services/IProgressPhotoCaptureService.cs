using Microsoft.Maui.ApplicationModel;
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
    /// exception handling, reused verbatim here). <paramref name="title"/> lets
    /// callers other than Measurements (e.g. a profile-picture picker) show
    /// wording that actually matches what's being captured.</summary>
    Task<byte[]?> CaptureAsync(string title = "Add Progress Photo");
}

public class ProgressPhotoCaptureService : IProgressPhotoCaptureService
{
    private const int MaxLongestEdge = 1200;
    private const int JpegQuality = 80;

    public async Task<byte[]?> CaptureAsync(string title = "Add Progress Photo")
    {
        var page = Shell.Current?.CurrentPage;
        if (page is null) return null;

        var choice = await page.DisplayActionSheetAsync(title, "Cancel", null, "Take Photo", "Choose from Library");
        if (choice is null || choice == "Cancel") return null;

        FileResult? result;
        try
        {
            if (choice == "Take Photo")
            {
                // Requesting this ourselves first (rather than letting
                // CapturePhotoAsync's own internal check run first) is
                // deliberate: on some devices/Android versions, granting the
                // OS prompt that CapturePhotoAsync triggers internally still
                // leaves it throwing PermissionException on the very next
                // call because its own status check reads a stale snapshot —
                // confirmed on a real Galaxy A11 (Android 10), where the
                // system Settings page already showed Camera as granted but
                // "Take Photo" kept failing with the permission error
                // regardless. Requesting up front, ourselves, sidesteps that.
                var status = await Permissions.RequestAsync<Permissions.Camera>();
                if (status != PermissionStatus.Granted)
                {
                    await page.DisplayAlertAsync("Permission needed",
                        "Camera permission is required to take a photo. If you've already granted it in Settings, try fully closing and reopening the app.", "OK");
                    return null;
                }
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
        using var codec = SKCodec.Create(sourceStream);
        using var original = codec is null ? SKBitmap.Decode(sourceStream) : SKBitmap.Decode(codec);
        if (original is null) return null;

        // Camera/gallery images commonly carry an EXIF orientation tag rather
        // than storing pixels upright — SKBitmap.Decode ignores it, which is
        // why an unrotated capture showed up sideways (confirmed on a real
        // device: a photo taken in portrait rendered rotated 90°).
        using var oriented = codec is null ? original.Copy() : ApplyExifOrientation(original, codec.EncodedOrigin);

        using var resized = ResizeToLongestEdge(oriented, MaxLongestEdge);
        using var image = SKImage.FromBitmap(resized);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
        return encoded.ToArray();
    }

    /// <summary>Redraws the decoded bitmap upright per its EXIF orientation tag. A no-op copy for the common TopLeft (already-upright) case.</summary>
    private static SKBitmap ApplyExifOrientation(SKBitmap bitmap, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft) return bitmap.Copy();

        var swapsDimensions = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var width = bitmap.Width;
        var height = bitmap.Height;
        var result = new SKBitmap(swapsDimensions ? height : width, swapsDimensions ? width : height);

        using var canvas = new SKCanvas(result);
        switch (origin)
        {
            case SKEncodedOrigin.TopRight: // mirrored horizontally
                canvas.Translate(width, 0);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.BottomRight: // upside down
                canvas.Translate(width, height);
                canvas.RotateDegrees(180);
                break;
            case SKEncodedOrigin.BottomLeft: // mirrored vertically
                canvas.Translate(0, height);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.LeftTop: // mirrored + rotated 90° CW
                canvas.RotateDegrees(90);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.RightTop: // rotated 90° CW — the common "portrait photo" case
                canvas.Translate(height, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightBottom: // mirrored + rotated 90° CCW
                canvas.Translate(height, width);
                canvas.RotateDegrees(90);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.LeftBottom: // rotated 90° CCW
                canvas.Translate(0, width);
                canvas.RotateDegrees(-90);
                break;
        }
        canvas.DrawBitmap(bitmap, 0, 0);
        return result;
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
