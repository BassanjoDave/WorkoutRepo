using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class LogMeasurementPage : ContentPage
{
    private readonly LogMeasurementViewModel _viewModel;

    public LogMeasurementPage(LogMeasurementViewModel viewModel)
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
