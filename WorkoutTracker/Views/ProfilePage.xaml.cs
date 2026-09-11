using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _viewModel;

    public ProfilePage(ProfileViewModel viewModel)
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
