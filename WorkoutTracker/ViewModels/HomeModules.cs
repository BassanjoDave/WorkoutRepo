namespace WorkoutTracker.ViewModels;

/// <summary>
/// The fixed list of known Home dashboard modules — single source of truth
/// for ids/titles/default order, used by both HomeViewModel (to synthesize
/// defaults for a member who's never customized) and CustomizeHomeViewModel.
/// "exercise" is handled as Home's permanent anchor section (it owns the
/// week/day picker that Nutrition/Measurements/History don't need), so it's
/// excluded here — only the independently-orderable summary cards are listed.
/// </summary>
public static class HomeModules
{
    public static readonly (string Id, string Title)[] All =
    {
        ("history", "History"),
        ("nutrition", "Nutrition"),
        ("measurements", "Measurements"),
        ("stacks", "Stacks"),
    };

    public static string TitleFor(string moduleId) => All.FirstOrDefault(m => m.Id == moduleId).Title ?? moduleId;
}
