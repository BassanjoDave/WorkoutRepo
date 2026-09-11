using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class MeasurementsSummaryView : ContentView
{
    private readonly MeasurementsSummaryViewModel _viewModel;

    public MeasurementsSummaryView(MeasurementsSummaryViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    public Task LoadAsync() => _viewModel.LoadAsync();
}
