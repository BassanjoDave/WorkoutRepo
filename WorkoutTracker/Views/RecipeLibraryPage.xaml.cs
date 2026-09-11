using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class RecipeLibraryPage : ContentPage
{
    private readonly RecipeLibraryViewModel _viewModel;

    public RecipeLibraryPage(RecipeLibraryViewModel viewModel)
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
