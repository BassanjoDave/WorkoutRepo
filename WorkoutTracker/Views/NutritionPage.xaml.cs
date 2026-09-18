using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class NutritionPage : ContentPage
{
    private readonly NutritionViewModel _viewModel;
    private readonly AppTabBar _appHeader;

    public NutritionPage(NutritionViewModel viewModel, AppTabBar appHeader)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        _appHeader = appHeader;
        _appHeader.ActiveRoute = "nutrition";
        AppHeaderHost.Content = _appHeader;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _appHeader.LoadAsync();
        await _viewModel.LoadAsync();
    }
}
