using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// Deliberately the simplest possible card — no BindableLayout, no nested
/// visibility toggling — used in place of the real Nutrition/Measurements/Stacks
/// summary card on Home when the active member lacks access. HomePage.xaml.cs
/// decides which of the two to add to ModuleCardsHost.Children; the real summary
/// views' own internal structure is never touched, keeping their existing
/// BindableLayout-heavy XAML (already a known-fragile area on WinUI) untouched.
/// </summary>
public partial class LockedModuleCardViewModel : ObservableObject
{
    [ObservableProperty] public partial string Title { get; set; } = "";
    [ObservableProperty] public partial string Message { get; set; } = "";
    public string UpgradeRoute { get; set; } = "upgrade";

    [RelayCommand]
    private async Task Open() => await Shell.Current.GoToAsync(UpgradeRoute);
}
