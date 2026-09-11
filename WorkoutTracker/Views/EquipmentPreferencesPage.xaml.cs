using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class EquipmentPreferencesPage : ContentPage
{
    private readonly EquipmentPreferencesViewModel _viewModel;

    public EquipmentPreferencesPage(EquipmentPreferencesViewModel viewModel)
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
