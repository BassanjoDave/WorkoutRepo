using WorkoutTracker.Services;
using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class StandardAddExercisePage : ContentPage
{
    private readonly StandardBuilderViewModel? _viewModel;

    public StandardAddExercisePage(IActiveRoutineBuilderContext context)
    {
        InitializeComponent();
        _viewModel = context.ActiveStandard;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // A no-op unless reappearing after pushing "exerciseEditor" for the picker's
        // "+ Add custom exercise…" row — see RefreshAfterReturnAsync's doc comment.
        if (_viewModel is not null) await _viewModel.RefreshAfterReturnAsync();
    }

    private async void OnBrowseExercisesTapped(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("browseExercises");

    private async void OnAddFromRitualTapped(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("addFromRitual");
}
