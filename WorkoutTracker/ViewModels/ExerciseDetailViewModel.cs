using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.ViewModels;

public partial class ExerciseDetailViewModel : ObservableObject, IQueryAttributable
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;
    private Guid _exerciseId;

    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial string Muscles { get; set; } = "";
    [ObservableProperty] public partial string Equipment { get; set; } = "";
    [ObservableProperty] public partial string Bench { get; set; } = "";
    [ObservableProperty] public partial string Accessory { get; set; } = "";
    [ObservableProperty] public partial string ArmPosition { get; set; } = "";
    [ObservableProperty] public partial List<string> Tips { get; set; } = new();
    [ObservableProperty] public partial List<ExercisePhotoViewModel> Images { get; set; } = new();
    public bool HasImages => Images.Count > 0;

    public ExerciseDetailViewModel(IActiveSessionService session, IWorkoutRepository repo)
    {
        _session = session;
        _repo = repo;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query) =>
        _exerciseId = Guid.Parse((string)query["exerciseId"]);

    partial void OnImagesChanged(List<ExercisePhotoViewModel> value) => OnPropertyChanged(nameof(HasImages));

    public async Task LoadAsync()
    {
        var account = _session.ActiveAccount;
        if (account is null) return;

        var shared = await _repo.GetSharedLibraryAsync(account.Id);
        var manufacturer = await _repo.GetManufacturerLibraryAsync();
        var exercise = shared.Exercises.FirstOrDefault(e => e.Id == _exerciseId)
            ?? manufacturer.Exercises.FirstOrDefault(e => e.Id == _exerciseId);
        if (exercise is null) return;

        Name = exercise.Name;
        Muscles = exercise.Muscles;
        Equipment = exercise.Equipment.ToString();
        Bench = exercise.Bench;
        Accessory = exercise.Accessory;
        ArmPosition = exercise.ArmPosition;
        Tips = exercise.Tips;
        Images = exercise.Images.Select(i => new ExercisePhotoViewModel(i.Key, i.Label ?? "")).ToList();
    }

    [RelayCommand]
    private async Task Back() => await Shell.Current.GoToAsync("..");
}

public class ExercisePhotoViewModel
{
    public string Source { get; }
    public string Label { get; }

    public ExercisePhotoViewModel(string source, string label)
    {
        Source = source;
        Label = label;
    }
}
