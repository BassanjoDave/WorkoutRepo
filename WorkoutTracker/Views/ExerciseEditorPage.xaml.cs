using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class ExerciseEditorPage : ContentPage
{
    public ExerciseEditorPage(ExerciseEditorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
