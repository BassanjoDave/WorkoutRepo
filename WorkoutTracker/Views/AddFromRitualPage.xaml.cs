using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class AddFromRitualPage : ContentPage
{
    private readonly AddFromRitualViewModel _viewModel;

    public AddFromRitualPage(AddFromRitualViewModel viewModel)
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

    private void OnRoutineTapped(object? sender, EventArgs e)
    {
        if ((sender as Border)?.BindingContext is not RitualPickRowViewModel row) return;
        _viewModel.SelectRoutine(row.Id);
    }
}
