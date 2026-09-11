using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class ExerciseDetailPage : ContentPage
{
    private readonly ExerciseDetailViewModel _viewModel;

    public ExerciseDetailPage(ExerciseDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
