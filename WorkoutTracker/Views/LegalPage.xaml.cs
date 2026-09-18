using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class LegalPage : ContentPage
{
    public LegalPage(LegalViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
