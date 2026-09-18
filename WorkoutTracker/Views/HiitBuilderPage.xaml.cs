using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class HiitBuilderPage : ContentPage
{
    private readonly HiitBuilderViewModel _viewModel;
    private bool _hasLoadedOnce;

    public HiitBuilderPage(HiitBuilderViewModel viewModel)
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

    private async void OnAddSectionTapped(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("hiitAddSection");

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

    private static async Task FlashAsync(Border border)
    {
        var original = border.Stroke;
        border.Stroke = new SolidColorBrush((Color)Application.Current!.Resources["ColorAccent"]);
        await Task.Delay(1500);
        border.Stroke = original;
    }
}
