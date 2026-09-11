using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class CustomizeHomePage : ContentPage
{
    private readonly CustomizeHomeViewModel _viewModel;

    public CustomizeHomePage(CustomizeHomeViewModel viewModel)
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
