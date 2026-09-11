using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class ProgressPhotoViewerPage : ContentPage
{
    private readonly ProgressPhotoViewerViewModel _viewModel;

    public ProgressPhotoViewerPage(ProgressPhotoViewerViewModel viewModel)
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
