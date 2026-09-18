using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class HiitBuilderPage : ContentPage
{
    private readonly HiitBuilderViewModel _viewModel;
    private readonly LibraryView _libraryView;

    public HiitBuilderPage(HiitBuilderViewModel viewModel, LibraryView libraryView)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _libraryView = libraryView;
        BindingContext = viewModel;

        _libraryView.ExercisePickMode = true;
        _libraryView.ExercisePicked += exercise => _viewModel.AddSectionFromLibrary(exercise.Name);
        LibraryHost.Content = _libraryView;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
        await _libraryView.LoadAsync();
    }
}
