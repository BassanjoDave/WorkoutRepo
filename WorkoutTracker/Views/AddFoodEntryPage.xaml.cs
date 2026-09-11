using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class AddFoodEntryPage : ContentPage
{
    private readonly AddFoodEntryViewModel _viewModel;

    public AddFoodEntryPage(AddFoodEntryViewModel viewModel)
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
