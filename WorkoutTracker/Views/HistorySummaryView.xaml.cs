using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class HistorySummaryView : ContentView
{
    private readonly HistorySummaryViewModel _viewModel;

    public HistorySummaryView(HistorySummaryViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    public Task LoadAsync() => _viewModel.LoadAsync();
}
