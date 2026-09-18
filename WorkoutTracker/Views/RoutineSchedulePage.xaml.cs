using WorkoutTracker.Services;

namespace WorkoutTracker.Views;

public partial class RoutineSchedulePage : ContentPage
{
    public RoutineSchedulePage(IActiveRoutineBuilderContext context)
    {
        InitializeComponent();
        ScheduleEditor.BindingContext = context.ActiveStandard?.Reminder ?? context.ActiveHiit?.Reminder;
    }
}
