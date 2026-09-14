using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Media;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;
using Visibility = WorkoutTracker.Models.Visibility;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// Creates a custom exercise. Category doubles as the session-behavior switch —
/// picking "Vibration Plate" or "Cardio" is what makes the session runner show
/// those input layouts instead of sets/reps, same as manufacturer content.
/// </summary>
public partial class ExerciseEditorViewModel : ObservableObject
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;
    private readonly IPendingExerciseBridge _pendingExerciseBridge;

    private static readonly string[] CategoryValues =
        { "Chest", "Shoulders", "Back", "Arms", "Abs", "Legs", "Full Body", "Cardio", "Vibration Plate" };
    private static readonly string[] EquipmentValues =
    {
        "Bowflex Rig", "Bodyweight", "Kettlebell", "Dumbbell", "Barbell", "Weight Bench",
        "Resistance Band", "Vibration Plate", "Stretch", "Cardio Rig", "Custom",
    };

    public string[] CategoryOptions => CategoryValues;
    public string[] EquipmentOptions => EquipmentValues;

    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial string SelectedCategory { get; set; } = "Chest";
    [ObservableProperty] public partial string SelectedEquipment { get; set; } = "Bodyweight";
    [ObservableProperty] public partial string Muscles { get; set; } = "";
    [ObservableProperty] public partial string TipsText { get; set; } = "";
    [ObservableProperty] public partial bool IsAccountShared { get; set; }
    [ObservableProperty] public partial string ErrorMessage { get; set; } = "";
    [ObservableProperty] public partial ObservableCollection<ExercisePhotoRowViewModel> Photos { get; set; } = new();

    public ExerciseEditorViewModel(IActiveSessionService session, IWorkoutRepository repo, IPendingExerciseBridge pendingExerciseBridge)
    {
        _session = session;
        _repo = repo;
        _pendingExerciseBridge = pendingExerciseBridge;
    }

    /// <summary>Offers camera or gallery, copies the chosen photo into local app storage, and defaults the first two labels to Start/Finish.</summary>
    [RelayCommand]
    private async Task AddPhoto()
    {
        var page = Shell.Current?.CurrentPage;
        if (page is null) return;

        var choice = await page.DisplayActionSheetAsync("Add Photo", "Cancel", null, "Take Photo", "Choose from Library");
        if (choice is null || choice == "Cancel") return;

        IReadOnlyList<FileResult> results;
        try
        {
            if (choice == "Take Photo")
            {
                var photo = await MediaPicker.Default.CapturePhotoAsync();
                results = photo is null ? Array.Empty<FileResult>() : new[] { photo };
            }
            else
            {
                results = await MediaPicker.Default.PickPhotosAsync();
            }
        }
        catch (FeatureNotSupportedException)
        {
            await page.DisplayAlertAsync("Not supported", "This device doesn't support that option.", "OK");
            return;
        }
        catch (PermissionException)
        {
            await page.DisplayAlertAsync("Permission needed", "Camera/Photos permission is required to add a picture.", "OK");
            return;
        }

        foreach (var result in results)
        {
            var dir = Path.Combine(FileSystem.AppDataDirectory, "exercise-images");
            Directory.CreateDirectory(dir);
            var destPath = Path.Combine(dir, $"{Guid.NewGuid()}.jpg");
            await using (var sourceStream = await result.OpenReadAsync())
            await using (var destStream = File.Create(destPath))
            {
                await sourceStream.CopyToAsync(destStream);
            }

            var defaultLabel = Photos.Count switch { 0 => "Start", 1 => "Finish", _ => "" };
            Photos.Add(new ExercisePhotoRowViewModel(destPath, defaultLabel, RemovePhotoCommand));
        }
    }

    [RelayCommand]
    private void RemovePhoto(ExercisePhotoRowViewModel photo)
    {
        Photos.Remove(photo);
        try { File.Delete(photo.Path); } catch { /* best-effort cleanup; a leftover file costs nothing */ }
    }

    [RelayCommand]
    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Give it a name first.";
            return;
        }

        var account = _session.ActiveAccount;
        var member = _session.ActiveMember;
        if (account is null || member is null) return;

        var shared = await _repo.GetSharedLibraryAsync(account.Id);
        var exercise = new Exercise
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            OwnerMemberId = member.Id,
            Name = Name.Trim(),
            Category = ParseCategory(SelectedCategory),
            Equipment = ParseEquipment(SelectedEquipment),
            Muscles = Muscles.Trim(),
            Tips = TipsText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            Visibility = IsAccountShared ? Visibility.Account : Visibility.Private,
            IsVibrationPlate = SelectedCategory == "Vibration Plate",
            IsCardio = SelectedCategory == "Cardio",
            Images = Photos.Select(p => new ExerciseImage { Key = p.Path, IsBundled = false, Label = string.IsNullOrWhiteSpace(p.Label) ? null : p.Label.Trim() }).ToList(),
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        shared.Exercises.Add(exercise);
        await _repo.SaveSharedLibraryAsync(account.Id, shared);
        _pendingExerciseBridge.SetPendingExerciseId(exercise.Id);
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task Cancel() => await Shell.Current.GoToAsync("..");

    private static ExerciseCategory ParseCategory(string value) => value switch
    {
        "Full Body" => ExerciseCategory.FullBody,
        "Vibration Plate" => ExerciseCategory.VibrationPlate,
        _ => Enum.Parse<ExerciseCategory>(value),
    };

    private static ExerciseEquipment ParseEquipment(string value) => value switch
    {
        "Bowflex Rig" => ExerciseEquipment.BowflexMachine,
        "Vibration Plate" => ExerciseEquipment.VibrationPlate,
        "Cardio Rig" => ExerciseEquipment.CardioMachine,
        "Weight Bench" => ExerciseEquipment.WeightBench,
        "Resistance Band" => ExerciseEquipment.ResistanceBand,
        _ => Enum.Parse<ExerciseEquipment>(value),
    };
}

public partial class ExercisePhotoRowViewModel : ObservableObject
{
    public string Path { get; }
    [ObservableProperty] public partial string Label { get; set; }
    public IRelayCommand<ExercisePhotoRowViewModel> RemoveCommand { get; }

    public ExercisePhotoRowViewModel(string path, string label, IRelayCommand<ExercisePhotoRowViewModel> removeCommand)
    {
        Path = path;
        Label = label;
        RemoveCommand = removeCommand;
    }
}
