using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class StandardBuilderPage : ContentPage
{
    private readonly StandardBuilderViewModel _viewModel;
    private bool _hasLoadedOnce;

    public StandardBuilderPage(StandardBuilderViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_hasLoadedOnce) return;
        _hasLoadedOnce = true;
        await _viewModel.LoadAsync();
    }

    private async void OnAddExerciseTapped(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("standardAddExercise");

    private async void OnScheduleTapped(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("routineSchedule");

    /// <summary>Called from Save() when the "Name already used" dialog's "Let me
    /// rename it" choice is picked — focusing also triggers the global
    /// select-all-on-focus Entry behavior, and the brief accent flash makes it
    /// obvious which box needs attention.</summary>
    public void FocusRoutineName()
    {
        RoutineNameEntry.Focus();
        _ = FlashAsync(RoutineNameBorder);
    }

    /// <summary>Called from Save() when "Missing details" → "Let me fix it" is picked.
    /// BindableLayout on a plain VerticalStackLayout (not CollectionView) realizes one
    /// real child View per item, in the same order as the bound collection — so the
    /// first incomplete row's View can be found by index and scrolled into view.</summary>
    public async Task ScrollToExerciseRow(int index)
    {
        if (index < 0 || index >= ExerciseRowsHost.Children.Count) return;
        if (ExerciseRowsHost.Children[index] is not Element target) return;
        await MainScrollView.ScrollToAsync(target, ScrollToPosition.Center, true);
    }

    private static async Task FlashAsync(Border border)
    {
        var original = border.Stroke;
        border.Stroke = new SolidColorBrush((Color)Application.Current!.Resources["ColorAccent"]);
        await Task.Delay(1500);
        border.Stroke = original;
    }
}
