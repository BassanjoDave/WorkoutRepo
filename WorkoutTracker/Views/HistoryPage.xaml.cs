using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class HistoryPage : ContentPage
{
    private readonly HistoryViewModel _viewModel;
    private readonly AppTabBar _appHeader;

    public HistoryPage(HistoryViewModel viewModel, AppTabBar appHeader)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        _appHeader = appHeader;
        _appHeader.ActiveRoute = "history";
        AppHeaderHost.Content = _appHeader;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _appHeader.LoadAsync();
        await _viewModel.LoadAsync();
    }
}
