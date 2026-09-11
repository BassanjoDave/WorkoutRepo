using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class OnboardingPage : ContentPage
{
    public OnboardingPage(OnboardingViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
