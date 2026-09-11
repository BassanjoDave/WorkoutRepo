using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class ExercisePage : ContentPage
{
    private readonly ExerciseViewModel _viewModel;
    private readonly WorkoutsView _workoutsView;
    private readonly LibraryView _libraryView;

    public ExercisePage(ExerciseViewModel viewModel, WorkoutsView workoutsView, LibraryView libraryView)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
        _workoutsView = workoutsView;
        _libraryView = libraryView;
        WorkoutsHost.Content = workoutsView;
        LibraryHost.Content = libraryView;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _workoutsView.LoadAsync();
        await _libraryView.LoadAsync();

        if (_viewModel.PendingEquipmentKey is string key)
        {
            _libraryView.PreSelectEquipment(key);
            _viewModel.ClearPendingEquipmentKey();
        }
    }
}
