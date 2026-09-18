using WorkoutTracker.Services;

namespace WorkoutTracker.Views;

public partial class HiitAddSectionPage : ContentPage
{
    public HiitAddSectionPage(IActiveRoutineBuilderContext context)
    {
        InitializeComponent();
        BindingContext = context.ActiveHiit;
    }

    private async void OnBrowseExercisesTapped(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("browseExercises");

    private async void OnAddFromRitualTapped(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("addFromRitual");
}
