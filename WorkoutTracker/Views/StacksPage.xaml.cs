using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class StacksPage : ContentPage
{
    private readonly StacksViewModel _viewModel;

    public StacksPage(StacksViewModel viewModel)
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
