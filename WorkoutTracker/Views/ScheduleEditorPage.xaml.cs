using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class ScheduleEditorPage : ContentPage
{
    private readonly ScheduleEditorViewModel _viewModel;

    public ScheduleEditorPage(ScheduleEditorViewModel viewModel)
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
