using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class StackEditorPage : ContentPage
{
    private readonly StackEditorViewModel _viewModel;

    public StackEditorPage(StackEditorViewModel viewModel)
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
