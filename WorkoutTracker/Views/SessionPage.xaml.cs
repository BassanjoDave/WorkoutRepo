using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class SessionPage : ContentPage
{
    private readonly SessionViewModel _viewModel;

    public SessionPage(SessionViewModel viewModel)
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
