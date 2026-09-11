using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class HiitPlayerPage : ContentPage
{
    private readonly HiitPlayerViewModel _viewModel;

    public HiitPlayerPage(HiitPlayerViewModel viewModel)
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

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.Dispose();
    }
}
