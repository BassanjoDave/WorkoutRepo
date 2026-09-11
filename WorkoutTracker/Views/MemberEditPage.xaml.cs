using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class MemberEditPage : ContentPage
{
    private readonly MemberEditViewModel _viewModel;

    public MemberEditPage(MemberEditViewModel viewModel)
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

    private void OnColorTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is string hex) _viewModel.SelectedColor = hex;
    }
}
