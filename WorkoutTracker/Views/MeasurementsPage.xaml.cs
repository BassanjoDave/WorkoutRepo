using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class MeasurementsPage : ContentPage
{
    private readonly MeasurementsViewModel _viewModel;

    public MeasurementsPage(MeasurementsViewModel viewModel)
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
