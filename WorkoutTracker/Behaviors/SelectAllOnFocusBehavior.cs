namespace WorkoutTracker.Behaviors;

/// <summary>
/// Selects an Entry's full text whenever it gains focus, so typing replaces
/// rather than appends. Stateless (only ever touches the `sender` passed to
/// it) so a single instance can be shared across every Entry via the app-wide
/// implicit Entry Style in Styles.xaml — it never stores a per-control
/// reference, which is what would otherwise stop one Behavior instance being
/// attached to more than one element at a time.
/// </summary>
public class SelectAllOnFocusBehavior : Behavior<Entry>
{
    protected override void OnAttachedTo(Entry bindable)
    {
        base.OnAttachedTo(bindable);
        bindable.Focused += OnFocused;
    }

    protected override void OnDetachingFrom(Entry bindable)
    {
        bindable.Focused -= OnFocused;
        base.OnDetachingFrom(bindable);
    }

    private static void OnFocused(object? sender, FocusEventArgs e)
    {
        if (sender is not Entry entry) return;
        entry.CursorPosition = 0;
        entry.SelectionLength = entry.Text?.Length ?? 0;
    }
}
