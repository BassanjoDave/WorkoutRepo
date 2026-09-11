using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class HiitBuilderPage : ContentPage
{
    private readonly HiitBuilderViewModel _viewModel;

    public HiitBuilderPage(HiitBuilderViewModel viewModel)
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
