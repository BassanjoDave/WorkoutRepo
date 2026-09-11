using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class ProfileGatePage : ContentPage
{
    private readonly ProfileGateViewModel _viewModel;

    public ProfileGatePage(ProfileGateViewModel viewModel)
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

    private async void OnDevSettingsClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("devSettings");
}
