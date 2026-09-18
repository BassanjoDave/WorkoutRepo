using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class StackEditorPage : ContentPage
{
    private readonly StackEditorViewModel _viewModel;

    public StackEditorPage(StackEditorViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    /// <summary>Called from Save() when the stack has no name — focusing also triggers
    /// the global select-all-on-focus Entry behavior, and the brief accent flash makes
    /// it obvious which box needs attention (same pattern as the Ritual Builder pages).</summary>
    public void FocusStackName()
    {
        StackNameEntry.Focus();
        _ = FlashAsync(StackNameBorder);
    }

    private static async Task FlashAsync(Border border)
    {
        var original = border.Stroke;
        border.Stroke = new SolidColorBrush((Color)Application.Current!.Resources["ColorAccent"]);
        await Task.Delay(1500);
        border.Stroke = original;
    }
}
