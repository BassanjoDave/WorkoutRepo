using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class ProgressPhotoGalleryPage : ContentPage
{
    private readonly ProgressPhotoGalleryViewModel _viewModel;

    public ProgressPhotoGalleryPage(ProgressPhotoGalleryViewModel viewModel)
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
