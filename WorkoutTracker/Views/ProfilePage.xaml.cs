using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _viewModel;
    private readonly AppTabBar _appHeader;

    public ProfilePage(ProfileViewModel viewModel, AppTabBar appHeader)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        _appHeader = appHeader;
        _appHeader.ActiveRoute = "profile";
        AppHeaderHost.Content = _appHeader;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _appHeader.LoadAsync();
        await _viewModel.LoadAsync();
    }

    private async void OnDevSettingsClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("devSettings");
}
