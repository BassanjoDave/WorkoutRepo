using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class FoodLibraryPage : ContentPage
{
    private readonly FoodLibraryViewModel _viewModel;

    public FoodLibraryPage(FoodLibraryViewModel viewModel)
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
