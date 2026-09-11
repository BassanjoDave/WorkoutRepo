using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// Pure tab-toggle state for the Exercise page — Workouts and Library keep
/// their own existing ViewModels untouched; this just decides which one is
/// visible. Kept as a small in-page toggle (matching History's Overview/
/// Progress/Log pattern) rather than Shell's native nested-Tab feature,
/// which doesn't render a switchable strip on Windows.
/// </summary>
public partial class ExerciseViewModel : ObservableObject, IQueryAttributable
{
    [ObservableProperty] public partial bool IsWorkoutsTab { get; set; } = true;
    public bool IsLibraryTab => !IsWorkoutsTab;

    /// <summary>Set by ApplyQueryAttributes when navigated here with an equipmentKey
    /// (e.g. from a My Rigs card) — read and cleared by ExercisePage.OnAppearing once
    /// it's been applied to the Library ViewModel, so it doesn't re-apply on every
    /// later, unrelated visit to this tab.</summary>
    public string? PendingEquipmentKey { get; private set; }

    public void ClearPendingEquipmentKey() => PendingEquipmentKey = null;

    partial void OnIsWorkoutsTabChanged(bool value) => OnPropertyChanged(nameof(IsLibraryTab));

    [RelayCommand] private void ShowWorkouts() => IsWorkoutsTab = true;
    [RelayCommand] private void ShowLibrary() => IsWorkoutsTab = false;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("equipmentKey", out var key))
        {
            PendingEquipmentKey = (string)key;
            IsWorkoutsTab = false;
        }
    }
}
