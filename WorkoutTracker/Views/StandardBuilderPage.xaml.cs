using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class StandardBuilderPage : ContentPage
{
    private readonly StandardBuilderViewModel _viewModel;
    private readonly LibraryView _libraryView;
    private bool _hasLoadedOnce;

    public StandardBuilderPage(StandardBuilderViewModel viewModel, LibraryView libraryView)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _libraryView = libraryView;
        BindingContext = viewModel;

        _libraryView.ExercisePickMode = true;
        _libraryView.ExercisePicked += exercise => _viewModel.AddExerciseFromLibrary(exercise.Id);
        LibraryHost.Content = _libraryView;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_hasLoadedOnce)
        {
            _hasLoadedOnce = true;
            await _viewModel.LoadAsync();
            await _libraryView.LoadAsync();
        }
        else
        {
            // Reappearing after pushing "exerciseEditor" (custom exercise from
            // the picker) — see RefreshAfterReturnAsync's doc comment for why
            // this must NOT be a full LoadAsync().
            await _viewModel.RefreshAfterReturnAsync();
        }
    }
}
