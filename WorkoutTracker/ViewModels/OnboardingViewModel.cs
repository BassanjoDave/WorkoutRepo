using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// A one-time, first-launch feature tour — see ProfileGateViewModel.LoadAsync for
/// how it's slotted in before the sign-in gate. Purely informational: Skip and
/// "Get Started" (shown only on the last slide) both just pop back to the gate.
/// </summary>
public partial class OnboardingViewModel : ObservableObject
{
    public List<OnboardingSlide> Slides { get; } = new()
    {
        new("Welcome to Rig Ritual",
            "Rig Ritual tailors standard exercises, stretches, and routines to your own Rig — the exact equipment you and your family train on — turning them into Workout Rituals built around what you actually have. Track workouts, nutrition, measurements, and supplements, all synced across your devices."),
        new("Exercises, Workouts, & HIIT Rituals",
            "Build custom Rituals, follow guided HIIT timers with color-coded sections, and log every set as you go."),
        new("Nutrition",
            "Log your nutritional intake with a large list of preloaded food items, with the flexibility to add your own foods for complete, accurate tracking. Choose from a vast assortment of preloaded recipes, modify them to suit your needs, or create new recipes from scratch."),
        new("Measurements",
            "Log and monitor your progress with body measurements and trend charts over time."),
        new("Stacks",
            "Keep track of the supplements and medications you take together, with reminders and an adherence history."),
        new("Built for families",
            "Add multiple family members under one account, each with their own profile and permissions — and share the Rituals, recipes, foods, and stacks any family member creates with the rest of the household."),
    };

    [ObservableProperty] public partial int CurrentIndex { get; set; }

    public bool IsLastSlide => CurrentIndex >= Slides.Count - 1;
    partial void OnCurrentIndexChanged(int value) => OnPropertyChanged(nameof(IsLastSlide));

    [RelayCommand]
    private async Task Finish() => await Shell.Current.GoToAsync("..");
}

public record OnboardingSlide(string Title, string Description);
