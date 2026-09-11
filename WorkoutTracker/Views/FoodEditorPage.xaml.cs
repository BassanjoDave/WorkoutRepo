using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class FoodEditorPage : ContentPage
{
    public FoodEditorPage(FoodEditorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
