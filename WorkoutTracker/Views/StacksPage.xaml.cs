using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class StacksPage : ContentPage
{
    private readonly StacksViewModel _viewModel;
    private readonly AppTabBar _appHeader;

    public StacksPage(StacksViewModel viewModel, AppTabBar appHeader)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        _appHeader = appHeader;
        _appHeader.ActiveRoute = "stacks";
        AppHeaderHost.Content = _appHeader;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _appHeader.LoadAsync();
        await _viewModel.LoadAsync();
    }
}
