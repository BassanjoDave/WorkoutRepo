using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class MacroGoalsPage : ContentPage
{
    private readonly MacroGoalsViewModel _viewModel;

    public MacroGoalsPage(MacroGoalsViewModel viewModel)
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
