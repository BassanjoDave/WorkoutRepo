using WorkoutTracker.ViewModels;

namespace WorkoutTracker.Views;

public partial class MeasurementsPage : ContentPage
{
    private readonly MeasurementsViewModel _viewModel;
    private readonly AppTabBar _appHeader;

    public MeasurementsPage(MeasurementsViewModel viewModel, AppTabBar appHeader)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        _appHeader = appHeader;
        _appHeader.ActiveRoute = "measurements";
        AppHeaderHost.Content = _appHeader;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _appHeader.LoadAsync();
        await _viewModel.LoadAsync();
    }

    /// <summary>
    /// Measurements is a TabBar-root ShellContent reached via an absolute
    /// "//measurements" navigation, so Shell has no back-stack entry to pop
    /// and never shows a NavBar back button — confirmed live that Shell's
    /// own Shell.BackButtonBehavior (see MeasurementsPage.xaml) is silently
    /// ignored there and Android's hardware back button falls through to the
    /// OS and exits the app outright. Overriding this lower-level, page-native
    /// hook (unlike BackButtonBehavior, always invoked for hardware/gesture
    /// back regardless of Shell's own back-stack bookkeeping) is what
    /// actually intercepts it.
    /// </summary>
    protected override bool OnBackButtonPressed()
    {
        _ = Shell.Current.GoToAsync("//profile");
        return true;
    }
}
