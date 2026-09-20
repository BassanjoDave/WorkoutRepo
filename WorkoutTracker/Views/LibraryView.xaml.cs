using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class LibraryView : ContentView
{
    private readonly LibraryViewModel _viewModel;

    /// <summary>When true (embedded in a workout builder — see StandardBuilderPage/
    /// HiitBuilderPage), tapping a row or its "+" button raises <see cref="ExercisePicked"/>
    /// instead of navigating to the exercise's detail page, and the per-row "+" button
    /// becomes visible as a second way to add an exercise alongside the builder's own
    /// dropdown.</summary>
    public static readonly BindableProperty ExercisePickModeProperty =
        BindableProperty.Create(nameof(ExercisePickMode), typeof(bool), typeof(LibraryView), false);
    public bool ExercisePickMode
    {
        get => (bool)GetValue(ExercisePickModeProperty);
        set => SetValue(ExercisePickModeProperty, value);
    }

    public event Action<ExerciseRowViewModel>? ExercisePicked;

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
        if (ExercisePickMode) { ExercisePicked?.Invoke(exercise); return; }
        await Shell.Current.GoToAsync($"exerciseDetail?exerciseId={exercise.Id}");
    }

    private async void OnAddCustomExerciseClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("exerciseEditor");
}
