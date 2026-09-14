using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class StandardBuilderPage : ContentPage
{
    private readonly StandardBuilderViewModel _viewModel;
    private bool _hasLoadedOnce;

    public StandardBuilderPage(StandardBuilderViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_hasLoadedOnce)
        {
            _hasLoadedOnce = true;
            await _viewModel.LoadAsync();
        }
        else
        {
            // Reappearing after pushing "exerciseEditor" (custom exercise from
            // the picker) — see RefreshAfterReturnAsync's doc comment for why
            // this must NOT be a full LoadAsync().
            await _viewModel.RefreshAfterReturnAsync();
        }
    }
}
