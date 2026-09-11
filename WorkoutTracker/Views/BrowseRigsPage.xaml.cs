using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class BrowseRigsPage : ContentPage
{
    private readonly BrowseRigsViewModel _viewModel;

    public BrowseRigsPage(BrowseRigsViewModel viewModel)
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
