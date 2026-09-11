using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// The 7-day × AM/PM toggle grid shared by both routine builders, matching
/// the original design's "assign this workout to a day right from the
/// builder" flow rather than requiring a separate trip to the schedule
/// editor. Applies to the active member's own schedule only.
/// </summary>
public partial class RoutineDayCellViewModel : ObservableObject
{
    public Weekday Day { get; }
    public Slot Slot { get; }
    public string Label { get; }

    [ObservableProperty] public partial bool IsSelected { get; set; }

    public RoutineDayCellViewModel(Weekday day, Slot slot, bool isSelected)
    {
        Day = day;
        Slot = slot;
        Label = $"{day.ToString()[..3]} {(slot == Slot.Am ? "AM" : "PM")}";
        IsSelected = isSelected;
    }

    [RelayCommand]
    private void Toggle() => IsSelected = !IsSelected;
}

public static class RoutineDayScheduleHelper
{
    public static List<RoutineDayCellViewModel> BuildCells(Schedule schedule, Guid routineId)
    {
        var cells = new List<RoutineDayCellViewModel>();
        foreach (Weekday day in Enum.GetValues<Weekday>())
        {
            schedule.Days.TryGetValue(day, out var slots);
            cells.Add(new RoutineDayCellViewModel(day, Slot.Am, slots?.Am.Contains(routineId) ?? false));
            cells.Add(new RoutineDayCellViewModel(day, Slot.Pm, slots?.Pm.Contains(routineId) ?? false));
        }
        return cells;
    }

    public static void ApplyCellsToSchedule(Schedule schedule, Guid routineId, IEnumerable<RoutineDayCellViewModel> cells)
    {
        foreach (var cell in cells)
        {
            if (!schedule.Days.TryGetValue(cell.Day, out var slots))
            {
                slots = new DaySlots();
                schedule.Days[cell.Day] = slots;
            }
            var list = cell.Slot == Slot.Am ? slots.Am : slots.Pm;
            list.Remove(routineId);
            if (cell.IsSelected) list.Add(routineId);
        }
        schedule.UpdatedAt = DateTimeOffset.UtcNow;
    }
}
