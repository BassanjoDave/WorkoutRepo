namespace WorkoutTracker.Views;

public partial class AppTabBar : ContentView
{
    public static readonly BindableProperty ActiveRouteProperty = BindableProperty.Create(
        nameof(ActiveRoute), typeof(string), typeof(AppTabBar), string.Empty,
        propertyChanged: (bindable, _, _) => ((AppTabBar)bindable).UpdateHighlight());

    public string ActiveRoute
    {
        get => (string)GetValue(ActiveRouteProperty);
        set => SetValue(ActiveRouteProperty, value);
    }

    public AppTabBar()
    {
        InitializeComponent();
    }

    private void UpdateHighlight()
    {
        var accent = (Color)Application.Current!.Resources["ColorAccent"];
        var unselected = (Color)Application.Current!.Resources["ColorNeutral500"];
        var divider = (Color)Application.Current!.Resources["ColorDivider"];

        foreach (var button in new[] { HomeButton, ExerciseButton, NutritionButton, HistoryButton, MeasurementsButton, StacksButton, ProfileButton })
        {
            var isActive = (string)button.CommandParameter == ActiveRoute;
            // Outline + text only, no fill — matches the selected-chip look used
            // elsewhere (e.g. ToggleChip), which stays outlined rather than solid
            // so it doesn't compete visually with the dark-theme shadow glow.
            button.TextColor = isActive ? accent : unselected;
            button.FontAttributes = isActive ? FontAttributes.Bold : FontAttributes.None;
            button.BorderColor = isActive ? accent : divider;
        }
    }

    private async void OnTabClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string route } || route == ActiveRoute) return;
        await Shell.Current.GoToAsync($"//{route}");
    }
}
