using WorkoutTracker.Services;

namespace WorkoutTracker.Views;

public partial class BrowseExercisesPage : ContentPage
{
    private readonly LibraryView _libraryView;

    public BrowseExercisesPage(IActiveRoutineBuilderContext context, LibraryView libraryView)
    {
        InitializeComponent();
        _libraryView = libraryView;
        _libraryView.ExercisePickMode = true;
        _libraryView.ExercisePicked += exercise =>
        {
            if (context.ActiveStandard is not null) context.ActiveStandard.AddExerciseFromLibrary(exercise.Id);
            else context.ActiveHiit?.AddSectionFromLibrary(exercise.Name);
        };
        LibraryHost.Content = _libraryView;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _libraryView.LoadAsync();
    }
}
