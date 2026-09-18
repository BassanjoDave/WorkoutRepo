using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class ExercisePage : ContentPage
{
    private readonly ExerciseViewModel _viewModel;
    private readonly AppTabBar _appHeader;
    private readonly WorkoutsView _workoutsView;
    private readonly LibraryView _libraryView;

    public ExercisePage(ExerciseViewModel viewModel, AppTabBar appHeader, WorkoutsView workoutsView, LibraryView libraryView)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
        _appHeader = appHeader;
        _appHeader.ActiveRoute = "exercise";
        AppHeaderHost.Content = _appHeader;
        _workoutsView = workoutsView;
        _libraryView = libraryView;
        WorkoutsHost.Content = workoutsView;
        LibraryHost.Content = libraryView;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _appHeader.LoadAsync();
        await _workoutsView.LoadAsync();
        await _libraryView.LoadAsync();

        if (_viewModel.PendingEquipmentKey is string key)
        {
            _libraryView.PreSelectEquipment(key);
            _viewModel.ClearPendingEquipmentKey();
        }
    }
}
