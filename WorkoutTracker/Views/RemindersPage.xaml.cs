using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class RemindersPage : ContentPage
{
    private readonly RemindersViewModel _viewModel;

    public RemindersPage(RemindersViewModel viewModel)
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
