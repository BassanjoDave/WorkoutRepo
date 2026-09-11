using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class MyRigsPage : ContentPage
{
    private readonly MyRigsViewModel _viewModel;

    public MyRigsPage(MyRigsViewModel viewModel)
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
