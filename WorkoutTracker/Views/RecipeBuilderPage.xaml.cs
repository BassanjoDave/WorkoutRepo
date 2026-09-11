using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class RecipeBuilderPage : ContentPage
{
    private readonly RecipeBuilderViewModel _viewModel;
    private bool _loaded;

    public RecipeBuilderPage(RecipeBuilderViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_loaded)
        {
            await _viewModel.LoadAsync();
            _loaded = true;
        }
        else
        {
            // Reappearing after "Can't find it? Add a new food" pops back to
            // this same page instance — refresh the food list instead of
            // LoadAsync, which would discard the in-progress draft.
            await _viewModel.RefreshAfterAddingFoodAsync();
        }
    }
}
