using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class DevSettingsPage : ContentPage
{
    private readonly DevSettingsViewModel _viewModel;

    public DevSettingsPage(DevSettingsViewModel viewModel)
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
