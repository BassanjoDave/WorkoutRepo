using WorkoutTracker.Services;
using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _viewModel;
    private readonly AppTabBar _appHeader;
    private readonly Dictionary<string, (View View, Func<Task> Load)> _moduleCards;
    private readonly Dictionary<string, LockedModuleCardView> _lockedCards;
    private List<string> _displayedSignature = new();

    public HomePage(HomeViewModel viewModel, AppTabBar appHeader, NutritionSummaryView nutritionSummary,
        MeasurementsSummaryView measurementsSummary, HistorySummaryView historySummary, StacksSummaryView stacksSummary)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;

        _appHeader = appHeader;
        _appHeader.ActiveRoute = "home";
        AppHeaderHost.Content = _appHeader;

        _moduleCards = new Dictionary<string, (View, Func<Task>)>
        {
            ["nutrition"] = (nutritionSummary, nutritionSummary.LoadAsync),
            ["measurements"] = (measurementsSummary, measurementsSummary.LoadAsync),
            ["history"] = (historySummary, historySummary.LoadAsync),
            ["stacks"] = (stacksSummary, stacksSummary.LoadAsync),
        };

        // Deliberately separate, simple ContentView instances (no BindableLayout, no
        // shared state with the real cards) shown in place of a gated module's real
        // summary card — see LockedModuleCardViewModel's comment for why this stays
        // fully independent of the real cards' XAML rather than toggling visibility
        // inside them.
        _lockedCards = new Dictionary<string, LockedModuleCardView>
        {
            ["nutrition"] = new LockedModuleCardView { BindingContext = new LockedModuleCardViewModel
                { Title = "NUTRITION", Message = "Upgrade to track meals and macros", UpgradeRoute = $"upgrade?page={PageEntitlements.Nutrition}" } },
            ["measurements"] = new LockedModuleCardView { BindingContext = new LockedModuleCardViewModel
                { Title = "MEASUREMENTS", Message = "Upgrade to track body measurements", UpgradeRoute = $"upgrade?page={PageEntitlements.Measurements}" } },
            ["stacks"] = new LockedModuleCardView { BindingContext = new LockedModuleCardViewModel
                { Title = "STACKS", Message = "Upgrade to track supplements and medications", UpgradeRoute = $"upgrade?page={PageEntitlements.Stacks}" } },
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _appHeader.LoadAsync();
        await _viewModel.LoadAsync();

        // Signature includes lock state, not just the enabled module id list, so a
        // Developer Settings entitlement change is picked up on the next Home visit
        // even though the enabled module set itself hasn't changed.
        var signature = _viewModel.EnabledModuleIds
            .Select(id => _lockedCards.ContainsKey(id) && !_viewModel.HasPageAccess(id) ? $"{id}:locked" : id)
            .ToList();

        // Only touch ModuleCardsHost's native children when the signature has actually
        // changed — clearing and rebuilding it on every single appearance (which
        // OnAppearing fires constantly, e.g. after any navigation back to Home) crashed
        // natively inside WinUI's own child-collection handling, the same class of bug
        // fixed elsewhere via replacing bound ObservableCollections instead of
        // Clear()+Add(); a Layout's Children list has no equivalent atomic-replace, so
        // avoiding the unnecessary clear is the fix.
        if (!_displayedSignature.SequenceEqual(signature))
        {
            ModuleCardsHost.Children.Clear();
            foreach (var moduleId in _viewModel.EnabledModuleIds)
            {
                if (_lockedCards.TryGetValue(moduleId, out var lockedView) && !_viewModel.HasPageAccess(moduleId))
                {
                    ModuleCardsHost.Children.Add(lockedView);
                    continue;
                }
                if (!_moduleCards.TryGetValue(moduleId, out var card)) continue;
                ModuleCardsHost.Children.Add(card.View);
            }
            _displayedSignature = signature;
        }

        foreach (var moduleId in _viewModel.EnabledModuleIds)
        {
            if (_lockedCards.ContainsKey(moduleId) && !_viewModel.HasPageAccess(moduleId)) continue;
            if (_moduleCards.TryGetValue(moduleId, out var card)) await card.Load();
        }
    }

    private async Task<bool> ConfirmIfNotTodayAsync()
    {
        var warning = _viewModel.PastOrFutureWarning();
        if (warning is null) return true;
        return await this.DisplayAlertAsync("Different day", warning, "Continue", "Cancel");
    }

    private async void OnStartClicked(object? sender, EventArgs e)
    {
        if ((sender as Button)?.BindingContext is not TodaySlotViewModel slot) return;
        if (!await ConfirmIfNotTodayAsync()) return;
        var route = slot.IsHiit ? "hiitPlayer" : "session";
        await Shell.Current.GoToAsync($"{route}?routineId={slot.RoutineId}&time={slot.Time:HH\\:mm}&date={_viewModel.SelectedDate:yyyy-MM-dd}");
    }

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        if ((sender as Button)?.BindingContext is not TodaySlotViewModel slot) return;
        if (!await ConfirmIfNotTodayAsync()) return;
        var route = slot.IsHiit ? "hiitBuilder" : "standardBuilder";
        await Shell.Current.GoToAsync($"{route}?routineId={slot.RoutineId}");
    }

    private async void OnBrowseWorkoutsClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//exercise");
    }

}
