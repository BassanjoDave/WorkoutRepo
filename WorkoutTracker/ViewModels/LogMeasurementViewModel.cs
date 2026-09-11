using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.ViewModels;

/// <summary>Upserts one BodyMeasurementEntry by date — logging again for a date you've already logged edits that entry rather than duplicating it.</summary>
public partial class LogMeasurementViewModel : ObservableObject
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;

    private Guid _accountId;
    private Guid _memberId;
    private MemberData _memberData = new();

    [ObservableProperty] public partial DateTime Date { get; set; } = DateTime.Today;
    [ObservableProperty] public partial string Weight { get; set; } = "";
    [ObservableProperty] public partial string Neck { get; set; } = "";
    [ObservableProperty] public partial string Shoulder { get; set; } = "";
    [ObservableProperty] public partial string Chest { get; set; } = "";
    [ObservableProperty] public partial string LBicep { get; set; } = "";
    [ObservableProperty] public partial string RBicep { get; set; } = "";
    [ObservableProperty] public partial string Waist { get; set; } = "";
    [ObservableProperty] public partial string Abdomen { get; set; } = "";
    [ObservableProperty] public partial string Hip { get; set; } = "";
    [ObservableProperty] public partial string LThigh { get; set; } = "";
    [ObservableProperty] public partial string RThigh { get; set; } = "";
    [ObservableProperty] public partial string LCalf { get; set; } = "";
    [ObservableProperty] public partial string RCalf { get; set; } = "";
    [ObservableProperty] public partial ObservableCollection<CustomMeasurementRowViewModel> CustomRows { get; set; } = new();
    [ObservableProperty] public partial string Notes { get; set; } = "";
    [ObservableProperty] public partial string WeightUnitLabel { get; set; } = "lbs";
    [ObservableProperty] public partial string ErrorMessage { get; set; } = "";

    public LogMeasurementViewModel(IActiveSessionService session, IWorkoutRepository repo)
    {
        _session = session;
        _repo = repo;
    }

    partial void OnDateChanged(DateTime value) => LoadEntryForDate();

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
        WeightUnitLabel = _memberData.WeightUnit;
        LoadEntryForDate();
    }

    private void LoadEntryForDate()
    {
        var date = DateOnly.FromDateTime(Date);
        var existing = _memberData.Measurements.FirstOrDefault(m => m.Date == date);
        Weight = existing?.Weight?.ToString("0.#") ?? "";
        Neck = existing?.Neck?.ToString("0.#") ?? "";
        Shoulder = existing?.Shoulder?.ToString("0.#") ?? "";
        Chest = existing?.Chest?.ToString("0.#") ?? "";
        LBicep = existing?.LBicep?.ToString("0.#") ?? "";
        RBicep = existing?.RBicep?.ToString("0.#") ?? "";
        Waist = existing?.Waist?.ToString("0.#") ?? "";
        Abdomen = existing?.Abdomen?.ToString("0.#") ?? "";
        Hip = existing?.Hip?.ToString("0.#") ?? "";
        LThigh = existing?.LThigh?.ToString("0.#") ?? "";
        RThigh = existing?.RThigh?.ToString("0.#") ?? "";
        LCalf = existing?.LCalf?.ToString("0.#") ?? "";
        RCalf = existing?.RCalf?.ToString("0.#") ?? "";
        Notes = existing?.Notes ?? "";

        // Replacing the whole collection (rather than Clear() + Add() in place) avoids
        // BindableLayout briefly seeing an empty source mid-rebuild — that transient
        // empty state crashes natively inside WinUI's own child-collection handling
        // (see the same fix in WorkoutsViewModel.Rebuild()).
        var customRows = new ObservableCollection<CustomMeasurementRowViewModel>();
        foreach (var slot in _memberData.CustomMeasurementSlots)
        {
            var valueText = existing is not null && existing.CustomValues.TryGetValue(slot.Id, out var v) ? v.ToString("0.#") : "";
            customRows.Add(new CustomMeasurementRowViewModel(slot.Id, slot.Name, valueText, RemoveCustomSlotCommand));
        }
        CustomRows = customRows;
    }

    [RelayCommand]
    private async Task AddCustomSlot()
    {
        if (Shell.Current?.CurrentPage is not Page page) return;
        var name = await page.DisplayPromptAsync("New measurement", "Name this measurement location:", "Add", "Cancel");
        if (string.IsNullOrWhiteSpace(name)) return;

        var slot = new CustomMeasurementSlot { Id = Guid.NewGuid(), Name = name.Trim() };
        _memberData.CustomMeasurementSlots.Add(slot);
        await _repo.SaveMemberDataAsync(_accountId, _memberId, _memberData);
        CustomRows.Add(new CustomMeasurementRowViewModel(slot.Id, slot.Name, "", RemoveCustomSlotCommand));
    }

    [RelayCommand]
    private async Task RemoveCustomSlot(CustomMeasurementRowViewModel row)
    {
        if (Shell.Current?.CurrentPage is not Page page) return;
        var confirmed = await page.DisplayAlertAsync("Remove measurement",
            $"Remove \"{row.Name}\" as a tracked measurement? This removes it — and its logged history — everywhere, not just today.",
            "Remove", "Cancel");
        if (!confirmed) return;

        _memberData.CustomMeasurementSlots.RemoveAll(s => s.Id == row.SlotId);
        foreach (var entry in _memberData.Measurements) entry.CustomValues.Remove(row.SlotId);
        await _repo.SaveMemberDataAsync(_accountId, _memberId, _memberData);
        CustomRows.Remove(row);
    }

    [RelayCommand]
    private async Task Save()
    {
        var weight = ParseNullableDouble(Weight);
        var neck = ParseNullableDouble(Neck);
        var shoulder = ParseNullableDouble(Shoulder);
        var chest = ParseNullableDouble(Chest);
        var lBicep = ParseNullableDouble(LBicep);
        var rBicep = ParseNullableDouble(RBicep);
        var waist = ParseNullableDouble(Waist);
        var abdomen = ParseNullableDouble(Abdomen);
        var hip = ParseNullableDouble(Hip);
        var lThigh = ParseNullableDouble(LThigh);
        var rThigh = ParseNullableDouble(RThigh);
        var lCalf = ParseNullableDouble(LCalf);
        var rCalf = ParseNullableDouble(RCalf);
        var customValues = CustomRows
            .Select(r => (r.SlotId, Value: ParseNullableDouble(r.ValueText)))
            .Where(r => r.Value is not null)
            .ToDictionary(r => r.SlotId, r => r.Value!.Value);

        if (weight is null && neck is null && shoulder is null && chest is null && lBicep is null && rBicep is null
            && waist is null && abdomen is null && hip is null && lThigh is null && rThigh is null && lCalf is null && rCalf is null
            && customValues.Count == 0)
        {
            ErrorMessage = "Enter at least one measurement.";
            return;
        }

        var date = DateOnly.FromDateTime(Date);
        var existing = _memberData.Measurements.FirstOrDefault(m => m.Date == date);
        if (existing is null)
        {
            existing = new BodyMeasurementEntry { Id = Guid.NewGuid(), Date = date };
            _memberData.Measurements.Add(existing);
        }
        existing.Weight = weight;
        existing.Neck = neck;
        existing.Shoulder = shoulder;
        existing.Chest = chest;
        existing.LBicep = lBicep;
        existing.RBicep = rBicep;
        existing.Waist = waist;
        existing.Abdomen = abdomen;
        existing.Hip = hip;
        existing.LThigh = lThigh;
        existing.RThigh = rThigh;
        existing.LCalf = lCalf;
        existing.RCalf = rCalf;
        existing.CustomValues = customValues;
        existing.Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim();
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _repo.SaveMemberDataAsync(_accountId, _memberId, _memberData);
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task Cancel() => await Shell.Current.GoToAsync("..");

    private static double? ParseNullableDouble(string value) => double.TryParse(value, out var d) ? d : null;
}

public partial class CustomMeasurementRowViewModel : ObservableObject
{
    public Guid SlotId { get; }
    public string Name { get; }
    public IRelayCommand<CustomMeasurementRowViewModel> RemoveCommand { get; }

    [ObservableProperty] public partial string ValueText { get; set; }

    public CustomMeasurementRowViewModel(Guid slotId, string name, string valueText, IRelayCommand<CustomMeasurementRowViewModel> removeCommand)
    {
        SlotId = slotId;
        Name = name;
        ValueText = valueText;
        RemoveCommand = removeCommand;
    }
}
