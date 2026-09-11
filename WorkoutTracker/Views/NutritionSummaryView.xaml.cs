using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class NutritionSummaryView : ContentView
{
    private readonly NutritionSummaryViewModel _viewModel;

    public NutritionSummaryView(NutritionSummaryViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    public Task LoadAsync() => _viewModel.LoadAsync();
}
