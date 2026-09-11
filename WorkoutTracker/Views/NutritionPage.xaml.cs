using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class NutritionPage : ContentPage
{
    private readonly NutritionViewModel _viewModel;

    public NutritionPage(NutritionViewModel viewModel)
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
