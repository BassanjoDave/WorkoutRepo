using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class StandardBuilderPage : ContentPage
{
    private readonly StandardBuilderViewModel _viewModel;

    public StandardBuilderPage(StandardBuilderViewModel viewModel)
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
