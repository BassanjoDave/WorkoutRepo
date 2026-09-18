using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;
using WorkoutTracker.Views;

namespace WorkoutTracker.ViewModels;

/// <summary>Add/edit one supplement-and-medication stack: its own days/time, its
/// items, and its reminder — see IStackReminderService for how the reminder fires.</summary>
public partial class StackEditorViewModel : ObservableObject, IQueryAttributable
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;
    private readonly IStackReminderService _reminders;

    private Guid? _editingStackId;
    private Guid _accountId;
    private Guid _memberId;
    private MemberData _memberData = new();

    [ObservableProperty] public partial string Title { get; set; } = "New Stack";
    [ObservableProperty] public partial string StackName { get; set; } = "";

    /// <summary>A single flat list — an "All" pseudo-cell (Day == null) followed by the
    /// 7 real weekdays — rendered by ONE FlexLayout/BindableLayout in the XAML. An
    /// earlier version put "All" in its own sibling FlexLayout next to a second,
    /// separately-wrapping FlexLayout for the day cells; nesting two Wrap FlexLayouts
    /// like that fights over each other's available width and scatters cells across
    /// rows unpredictably. One flat bound collection avoids that entirely.</summary>
    [ObservableProperty] public partial ObservableCollection<StackDayCellViewModel> DayCells { get; set; } = new();

    [ObservableProperty] public partial TimeSpan Time { get; set; } = new(8, 0, 0);
    [ObservableProperty] public partial ObservableCollection<StackItemRowViewModel> Items { get; set; } = new();

    [ObservableProperty] public partial bool ReminderEnabled { get; set; }
    [ObservableProperty] public partial bool SnoozeEnabled { get; set; } = true;
    [ObservableProperty] public partial string SnoozeMinutesText { get; set; } = "10";

    // "Add item" form fields — cleared after each successful AddItem.
    [ObservableProperty] public partial string NewItemName { get; set; } = "";
    [ObservableProperty] public partial string NewItemKind { get; set; } = "";
    [ObservableProperty] public partial string NewItemForm { get; set; } = "";
    [ObservableProperty] public partial string NewItemDoseQuantity { get; set; } = "";
    [ObservableProperty] public partial string NewItemDoseUnit { get; set; } = "";
    [ObservableProperty] public partial string NewItemQuantityTaken { get; set; } = "1";
    [ObservableProperty] public partial string NewItemTakenUnit { get; set; } = "";
    [ObservableProperty] public partial string NewItemNote { get; set; } = "";
    [ObservableProperty] public partial string ErrorMessage { get; set; } = "";

    public string[] KindOptions => StackOptions.Kinds;
    public string[] FormOptions => StackOptions.Forms;
    public string[] DoseUnitOptions => StackOptions.DoseUnits;
    public string[] TakenUnitOptions => StackOptions.TakenUnits;

    /// <summary>Live preview of the just-typed quantity/unit, e.g. "2 Softgels" —
    /// see StackItemRowViewModel.TakenSummary for the identical logic on saved rows.</summary>
    public string NewItemTakenSummary => $"{NewItemQuantityTaken} {Pluralizer.Pluralize(NewItemTakenUnit, ParseDouble(NewItemQuantityTaken, 1))}";
    partial void OnNewItemQuantityTakenChanged(string value) => OnPropertyChanged(nameof(NewItemTakenSummary));
    partial void OnNewItemTakenUnitChanged(string value) => OnPropertyChanged(nameof(NewItemTakenSummary));

    public StackEditorViewModel(IActiveSessionService session, IWorkoutRepository repo, IStackReminderService reminders)
    {
        _session = session;
        _repo = repo;
        _reminders = reminders;
        NewItemKind = StackOptions.Kinds[0];
        NewItemForm = StackOptions.Forms[0];
        NewItemDoseUnit = StackOptions.DoseUnits[0];
        NewItemTakenUnit = StackOptions.TakenUnits[0];
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query) =>
        _editingStackId = query.TryGetValue("stackId", out var id) ? Guid.Parse((string)id) : null;

    public async Task LoadAsync()
    {
        var account = _session.ActiveAccount;
        var member = _session.ActiveMember;
        if (account is null || member is null)
        {
            await Shell.Current.GoToAsync("//gate");
            return;
        }
        _accountId = account.Id;
        _memberId = member.Id;
        _memberData = await _repo.GetMemberDataAsync(account.Id, member.Id);

        var stack = _editingStackId is Guid id ? _memberData.Stacks.FirstOrDefault(s => s.Id == id) : null;

        var selectedDays = stack?.Days ?? new HashSet<Weekday>();
        var isAllDays = selectedDays.Count == 0 || selectedDays.Count == 7;
        // Replacing the whole collection (rather than Clear() + Add() in place) avoids
        // BindableLayout briefly seeing an empty source mid-rebuild — that transient
        // empty state crashes natively inside WinUI's own child-collection handling
        // (see the same fix in WorkoutsViewModel.Rebuild()).
        var dayCells = new ObservableCollection<StackDayCellViewModel>
        {
            new(null, "All", isAllDays, ToggleDayCommand),
        };
        foreach (Weekday day in Enum.GetValues<Weekday>())
        {
            dayCells.Add(new StackDayCellViewModel(day, day.ToString()[..3], isAllDays || selectedDays.Contains(day), ToggleDayCommand));
        }
        DayCells = dayCells;

        if (stack is null)
        {
            Title = "New Stack";
            StackName = "";
            Time = new TimeSpan(8, 0, 0);
            Items = new ObservableCollection<StackItemRowViewModel>();
            ReminderEnabled = false;
            SnoozeEnabled = true;
            SnoozeMinutesText = "10";
            return;
        }

        Title = $"Edit {stack.Name}";
        StackName = stack.Name;
        Time = stack.Time.ToTimeSpan();
        var items = new ObservableCollection<StackItemRowViewModel>();
        foreach (var item in stack.Items) items.Add(new StackItemRowViewModel(item, RemoveItemCommand));
        Items = items;
        ReminderEnabled = stack.ReminderEnabled;
        SnoozeEnabled = stack.SnoozeEnabled;
        SnoozeMinutesText = stack.SnoozeMinutes.ToString();
    }

    [RelayCommand]
    private void ToggleDay(StackDayCellViewModel cell)
    {
        if (cell.Day is null)
        {
            var makeAllSelected = !cell.IsSelected;
            foreach (var c in DayCells) c.IsSelected = makeAllSelected;
            return;
        }

        cell.IsSelected = !cell.IsSelected;
        var allCell = DayCells.FirstOrDefault(c => c.Day is null);
        if (allCell is not null) allCell.IsSelected = DayCells.Where(c => c.Day is not null).All(c => c.IsSelected);
    }

    [RelayCommand]
    private void AddItem()
    {
        if (string.IsNullOrWhiteSpace(NewItemName))
        {
            ErrorMessage = "Give the item a name first.";
            return;
        }
        ErrorMessage = "";

        var item = new StackItem
        {
            Id = Guid.NewGuid(),
            Name = NewItemName.Trim(),
            Kind = NewItemKind,
            Form = NewItemForm,
            DoseQuantity = ParseDouble(NewItemDoseQuantity),
            DoseUnit = NewItemDoseUnit,
            QuantityTaken = ParseDouble(NewItemQuantityTaken, 1),
            TakenUnit = NewItemTakenUnit,
            Note = NewItemNote.Trim(),
        };
        Items.Add(new StackItemRowViewModel(item, RemoveItemCommand));

        NewItemName = "";
        NewItemDoseQuantity = "";
        NewItemQuantityTaken = "1";
        NewItemNote = "";
    }

    [RelayCommand]
    private void RemoveItem(StackItemRowViewModel row) => Items.Remove(row);

    [RelayCommand]
    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(StackName))
        {
            if (Shell.Current?.CurrentPage is StackEditorPage page)
            {
                await page.DisplayAlertAsync("Name it first", "Give this stack a name.", "OK");
                page.FocusStackName();
            }
            else
            {
                ErrorMessage = "Give this stack a name.";
            }
            return;
        }
        if (Items.Count == 0)
        {
            ErrorMessage = "Add at least one supplement or medication.";
            return;
        }
        var selectedDays = DayCells.Where(c => c.Day is not null && c.IsSelected).Select(c => c.Day!.Value).ToHashSet();
        if (selectedDays.Count == 0)
        {
            ErrorMessage = "Pick at least one day, or choose All.";
            return;
        }
        // Stored as empty specifically to mean "every day" (see SupplementStack.Days) —
        // keeps IStackReminderService.DaysFor's fallback in sync with what's saved here.
        var days = selectedDays.Count == 7 ? new HashSet<Weekday>() : selectedDays;

        var existing = _editingStackId is Guid id ? _memberData.Stacks.FirstOrDefault(s => s.Id == id) : null;
        var stack = existing ?? new SupplementStack { Id = Guid.NewGuid() };
        stack.Name = StackName.Trim();
        stack.Days = days;
        stack.Time = TimeOnly.FromTimeSpan(Time);
        stack.Items = Items.Select(row => row.ToModel()).ToList();
        stack.ReminderEnabled = ReminderEnabled;
        stack.SnoozeEnabled = SnoozeEnabled;
        stack.SnoozeMinutes = int.TryParse(SnoozeMinutesText, out var minutes) ? minutes : 10;
        stack.UpdatedAt = DateTimeOffset.UtcNow;

        if (existing is null) _memberData.Stacks.Add(stack);
        await _repo.SaveMemberDataAsync(_accountId, _memberId, _memberData);
        await _reminders.RescheduleAllAsync(_memberData.Stacks);

        await Shell.Current!.GoToAsync("..");
    }

    [RelayCommand]
    private async Task Cancel() => await Shell.Current.GoToAsync("..");

    private static double ParseDouble(string value, double fallback = 0) => double.TryParse(value, out var d) ? d : fallback;
}

/// <summary>Regular English "+s" pluralization — every word in StackOptions.TakenUnits
/// is a plain noun that pluralizes this way (no irregulars like "mouse"/"mice" in the list).</summary>
public static class Pluralizer
{
    public static string Pluralize(string singular, double quantity) =>
        quantity == 1 ? singular : singular + "s";
}

public partial class StackDayCellViewModel : ObservableObject
{
    /// <summary>Null means the "All" pseudo-cell — see StackEditorViewModel.DayCells.</summary>
    public Weekday? Day { get; }
    public string Label { get; }
    public IRelayCommand<StackDayCellViewModel> ToggleCommand { get; }

    [ObservableProperty] public partial bool IsSelected { get; set; }

    public StackDayCellViewModel(Weekday? day, string label, bool isSelected, IRelayCommand<StackDayCellViewModel> toggleCommand)
    {
        Day = day;
        Label = label;
        IsSelected = isSelected;
        ToggleCommand = toggleCommand;
    }
}

public partial class StackItemRowViewModel : ObservableObject
{
    public Guid Id { get; }
    public IRelayCommand<StackItemRowViewModel> RemoveCommand { get; }

    public string[] KindOptions => StackOptions.Kinds;
    public string[] FormOptions => StackOptions.Forms;
    public string[] DoseUnitOptions => StackOptions.DoseUnits;
    public string[] TakenUnitOptions => StackOptions.TakenUnits;

    [ObservableProperty] public partial string Name { get; set; }
    [ObservableProperty] public partial string Kind { get; set; }
    [ObservableProperty] public partial string Form { get; set; }
    [ObservableProperty] public partial string DoseQuantityText { get; set; }
    [ObservableProperty] public partial string DoseUnit { get; set; }
    [ObservableProperty] public partial string QuantityTakenText { get; set; }
    [ObservableProperty] public partial string TakenUnit { get; set; }
    [ObservableProperty] public partial string Note { get; set; }

    /// <summary>Live "2 Softgels" / "1 Pill" preview — updates as QuantityTakenText or
    /// TakenUnit change, since a Picker can't display a pluralized form of its own
    /// selected (singular) value without breaking two-way binding.</summary>
    public string TakenSummary => $"{QuantityTakenText} {Pluralizer.Pluralize(TakenUnit, ParseDouble(QuantityTakenText, 1))}";
    partial void OnQuantityTakenTextChanged(string value) => OnPropertyChanged(nameof(TakenSummary));
    partial void OnTakenUnitChanged(string value) => OnPropertyChanged(nameof(TakenSummary));

    public StackItemRowViewModel(StackItem item, IRelayCommand<StackItemRowViewModel> removeCommand)
    {
        Id = item.Id;
        Name = item.Name;
        Kind = item.Kind;
        Form = item.Form;
        DoseQuantityText = item.DoseQuantity.ToString("0.##");
        DoseUnit = item.DoseUnit;
        QuantityTakenText = item.QuantityTaken.ToString("0.##");
        TakenUnit = item.TakenUnit;
        Note = item.Note;
        RemoveCommand = removeCommand;
    }

    public StackItem ToModel() => new()
    {
        Id = Id,
        Name = Name.Trim(),
        Kind = Kind,
        Form = Form,
        DoseQuantity = ParseDouble(DoseQuantityText),
        DoseUnit = DoseUnit,
        QuantityTaken = ParseDouble(QuantityTakenText, 1),
        TakenUnit = TakenUnit,
        Note = Note.Trim(),
    };

    private static double ParseDouble(string value, double fallback = 0) => double.TryParse(value, out var d) ? d : fallback;
}
