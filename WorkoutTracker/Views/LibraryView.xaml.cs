using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class LibraryView : ContentView
{
    private readonly LibraryViewModel _viewModel;

    public LibraryView(LibraryViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    public Task LoadAsync() => _viewModel.LoadAsync();

    public void PreSelectEquipment(string equipmentKey) => _viewModel.PreSelectEquipment(equipmentKey);

    private async void OnExerciseTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Border)?.BindingContext is not ExerciseRowViewModel exercise) return;
        await Shell.Current.GoToAsync($"exerciseDetail?exerciseId={exercise.Id}");
    }

    private async void OnAddCustomExerciseClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("exerciseEditor");
}
