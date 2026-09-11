using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class UpgradePage : ContentPage
{
    public UpgradePage(UpgradeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
