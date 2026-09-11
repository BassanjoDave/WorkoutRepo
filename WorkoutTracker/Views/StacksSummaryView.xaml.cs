using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class StacksSummaryView : ContentView
{
    private readonly StacksSummaryViewModel _viewModel;

    public StacksSummaryView(StacksSummaryViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    public Task LoadAsync() => _viewModel.LoadAsync();
}
