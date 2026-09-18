using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class WorkoutsView : ContentView
{
    private readonly WorkoutsViewModel _viewModel;

    public WorkoutsView(WorkoutsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    public Task LoadAsync() => _viewModel.LoadAsync();

    private async void OnNewClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("standardBuilder");

    private async void OnNewHiitClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("hiitBuilder");

    private async void OnRoutineTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Border)?.BindingContext is not RoutineRowViewModel routine) return;
        var route = routine.IsHiit ? "hiitPlayer" : "session";
        // Starting a routine directly from the Rituals list (not from a scheduled
        // Home slot) has no "assigned" time to carry over — use right now.
        var now = TimeOnly.FromDateTime(DateTime.Now);
        await Shell.Current.GoToAsync($"{route}?routineId={routine.Id}&time={now:HH\\:mm}");
    }

    private async void OnEditRoutineClicked(object? sender, EventArgs e)
    {
        if ((sender as Button)?.BindingContext is not RoutineRowViewModel routine) return;
        var route = routine.IsHiit ? "hiitBuilder" : "standardBuilder";
        await Shell.Current.GoToAsync($"{route}?routineId={routine.Id}");
    }

    private async void OnCopyRoutineClicked(object? sender, EventArgs e)
    {
        if ((sender as Button)?.BindingContext is not RoutineRowViewModel routine) return;
        await _viewModel.CopyToMineCommand.ExecuteAsync(routine.Id);
    }
}
