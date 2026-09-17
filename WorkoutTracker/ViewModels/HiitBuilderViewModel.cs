using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;
using Visibility = WorkoutTracker.Models.Visibility;

namespace WorkoutTracker.ViewModels;

public partial class HiitBuilderViewModel : ObservableObject, IQueryAttributable
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;
    private readonly IWorkoutReminderService _reminders;
    private readonly IHomeWorkoutBridge _homeWorkoutBridge;

    private Guid? _editingRoutineId;
    private bool _fromHome;
    private bool _isManufacturerFork;
    private Guid _routineId;
    private Guid _accountId;
    private Guid _memberId;
    private string _memberName = "";
    private SharedLibrary _shared = new();
    private ManufacturerLibrary _manufacturer = new();
    private MemberData _memberData = new();

    public RoutineReminderEditorViewModel Reminder { get; }

    /// <summary>Set only when opened from a running HIIT session's Edit button — see HiitPlayerViewModel.EditWorkout.</summary>
    private DateOnly? _occDate;
    private Slot? _occSlot;

    // MAUI's XAML binding only resolves properties, not fields — this must
    // stay a property or the section-type Picker silently binds to nothing.
    public string[] SectionTypes { get; } = { "Warm Up", "Exercise", "Rest", "Cool Down", "Custom" };

    [ObservableProperty] public partial string RoutineName { get; set; } = "New HIIT Workout";
    [ObservableProperty] public partial bool IsAccountShared { get; set; }
    [ObservableProperty] public partial int Cycles { get; set; } = 1;
    [ObservableProperty] public partial int RestBetweenCycles { get; set; }
    [ObservableProperty] public partial ObservableCollection<HiitSectionRowViewModel> Sections { get; set; } = new();

    [ObservableProperty] public partial string NewSectionType { get; set; } = "Exercise";
    [ObservableProperty] public partial string NewSectionTitle { get; set; } = "";
    [ObservableProperty] public partial string NewSectionDescription { get; set; } = "";
    [ObservableProperty] public partial int NewSectionSeconds { get; set; } = 30;
    [ObservableProperty] public partial ObservableCollection<RoutineDayCellViewModel> DayCells { get; set; } = new();

    public HiitBuilderViewModel(IActiveSessionService session, IWorkoutRepository repo, IWorkoutReminderService reminders,
        IHomeWorkoutBridge homeWorkoutBridge)
    {
        _session = session;
        _repo = repo;
        _reminders = reminders;
        _homeWorkoutBridge = homeWorkoutBridge;
        Reminder = new RoutineReminderEditorViewModel(reminders);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _editingRoutineId = query.TryGetValue("routineId", out var id) ? Guid.Parse((string)id) : null;
        _occDate = query.TryGetValue("occDate", out var od) ? DateOnly.Parse((string)od) : null;
        _occSlot = query.TryGetValue("occSlot", out var os) ? Enum.Parse<Slot>((string)os) : null;
        _fromHome = query.TryGetValue("fromHome", out var fh) && (string)fh == "true";
    }

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
        _memberName = member.DisplayName;

        _shared = await _repo.GetSharedLibraryAsync(account.Id);
        _manufacturer = await _repo.GetManufacturerLibraryAsync();
        _memberData = await _repo.GetMemberDataAsync(account.Id, member.Id);
        _routineId = _editingRoutineId ?? Guid.NewGuid();
        DayCells = new ObservableCollection<RoutineDayCellViewModel>(RoutineDayScheduleHelper.BuildCells(_memberData.Schedule, _routineId));
        Reminder.Load(_memberData, _routineId);

        if (_editingRoutineId is not Guid routineId) return;

        var routine = _shared.Routines.FirstOrDefault(r => r.Id == routineId);
        if (routine is null)
        {
            // Home lets a member schedule a manufacturer routine directly (see
            // HomeViewModel.AddWorkout), so "Edit" here can land on one. It can't
            // be mutated in place — Save() below forks it into a private copy
            // instead, the same way a mid-session "just this occurrence" edit does.
            routine = _manufacturer.Routines.FirstOrDefault(r => r.Id == routineId);
            if (routine is null) return;
            _isManufacturerFork = true;
        }

        RoutineName = routine.Name;
        IsAccountShared = routine.Visibility == Visibility.Account;
        Cycles = routine.CycleRepeats ?? 1;
        RestBetweenCycles = routine.RestBetweenCyclesSeconds ?? 0;
        // Replacing the whole collection (rather than Clear() + Add() in place) avoids
        // BindableLayout briefly seeing an empty source mid-rebuild — that transient
        // empty state crashes natively inside WinUI's own child-collection handling
        // (see the same fix in WorkoutsViewModel.Rebuild()).
        var sections = new ObservableCollection<HiitSectionRowViewModel>();
        foreach (var s in routine.Sections ?? new())
        {
            sections.Add(new HiitSectionRowViewModel(s.Type, s.Title, s.Description, s.Seconds, HiitSectionColors.Resolve(s), RemoveSectionCommand));
        }
        Sections = sections;
    }

    [RelayCommand]
    private void AddSection()
    {
        if (NewSectionSeconds <= 0) return;
        var title = string.IsNullOrWhiteSpace(NewSectionTitle) ? NewSectionType : NewSectionTitle;
        Sections.Add(new HiitSectionRowViewModel(NewSectionType, title, NewSectionDescription, NewSectionSeconds,
            HiitSectionColors.DefaultFor(NewSectionType), RemoveSectionCommand));
        NewSectionTitle = "";
        NewSectionDescription = "";
        NewSectionSeconds = 30;
    }

    [RelayCommand]
    private void RemoveSection(HiitSectionRowViewModel row) => Sections.Remove(row);

    private List<HiitSection> BuildSections() => Sections.Select(s => new HiitSection
    {
        Id = Guid.NewGuid(),
        Type = s.Type,
        Title = s.Title,
        Description = s.Description,
        Seconds = s.Seconds,
        Color = s.Color,
    }).ToList();

    private const string DefaultRoutineName = "New HIIT Workout";

    [RelayCommand]
    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(RoutineName) || Sections.Count == 0) return;
        if (Shell.Current?.CurrentPage is not Page page) return;

        var now = DateTimeOffset.UtcNow;
        var existing = _editingRoutineId is Guid id ? _shared.Routines.FirstOrDefault(r => r.Id == id) : null;

        var trimmedName = RoutineName.Trim();
        if (string.Equals(trimmedName, DefaultRoutineName, StringComparison.OrdinalIgnoreCase))
        {
            await page.DisplayAlertAsync("Name it first",
                $"\"{DefaultRoutineName}\" is just the placeholder — give this workout a real name.", "OK");
            return;
        }

        // A "copy to mine" fork still displays under the source's own name until
        // an exercise is actually added or removed — see RoutineNamingHelper.
        // Once that happens it's promoted to a fully independent custom workout:
        // auto-renamed "[member]'s [title]" (unless already renamed away from
        // the source's name) and no longer exempted from the uniqueness check below.
        var currentKeys = RoutineNamingHelper.ExerciseKeysFor(RoutineType.Hiit, Enumerable.Empty<Guid>(), BuildSections());
        var stillShadowingSource = existing?.SourceExerciseKeysSnapshot is not null;
        if (stillShadowingSource && RoutineNamingHelper.HasDivergedFromSource(existing!.SourceExerciseKeysSnapshot, currentKeys))
        {
            if (string.Equals(trimmedName, existing.Name, StringComparison.OrdinalIgnoreCase))
            {
                trimmedName = RoutineNamingHelper.ApplyOwnerPrefix(_memberName, trimmedName);
                RoutineName = trimmedName;
            }
            existing.SourceExerciseKeysSnapshot = null;
            stillShadowingSource = false;
        }

        // Excludes the routine actually being overwritten in place, but NOT a
        // manufacturer routine being forked (existing is null then) — forking
        // into a same-named private copy would leave two identically-named
        // routines visible to this member, exactly the confusion this guards against.
        // Also skipped while stillShadowingSource: matching the source's name on
        // an unmodified copy is the intended behavior, not a collision.
        var others = _shared.Routines.Concat(_manufacturer.Routines)
            .Where(r => existing is null || r.Id != existing.Id).ToList();
        if (!stillShadowingSource && others.Any(r => string.Equals(r.Name, trimmedName, StringComparison.OrdinalIgnoreCase)))
        {
            var suggestion = SuggestUniqueName(trimmedName, others);
            var useSuggestion = await page.DisplayAlertAsync("Name already used",
                $"A workout named \"{trimmedName}\" already exists. Use \"{suggestion}\" instead?",
                $"Use \"{suggestion}\"", "Let me rename it");
            if (!useSuggestion) return;
            RoutineName = suggestion;
            trimmedName = suggestion;
        }

        var justThisOccurrence = false;
        if (_occDate is not null && _occSlot is not null)
        {
            justThisOccurrence = await page.DisplayAlertAsync("Save changes",
                "Apply this change to just this occurrence, or to every future occurrence of this workout?",
                "Just this occurrence", "All future occurrences");
        }

        if (justThisOccurrence && _occDate is DateOnly occDate && _occSlot is Slot occSlot)
        {
            var fork = new RoutineDefinition
            {
                Id = Guid.NewGuid(),
                AccountId = _accountId,
                OwnerMemberId = _memberId,
                OwnerNameSnapshot = _memberName,
                SourceRoutineId = _editingRoutineId,
                SourceNameSnapshot = existing?.OwnerNameSnapshot ?? existing?.Name,
                Name = RoutineName.Trim(),
                Visibility = Visibility.Private,
                Type = RoutineType.Hiit,
                CycleRepeats = Cycles,
                RestBetweenCyclesSeconds = RestBetweenCycles,
                Sections = BuildSections(),
                UpdatedAt = now,
            };
            _shared.Routines.Add(fork);
            await _repo.SaveSharedLibraryAsync(_accountId, _shared);

            _memberData.Schedule.OneTimeOverrides.RemoveAll(o => o.Date == occDate && o.Slot == occSlot &&
                (o.RoutineId == _editingRoutineId || _shared.Routines.FirstOrDefault(r => r.Id == o.RoutineId)?.SourceRoutineId == _editingRoutineId));
            _memberData.Schedule.OneTimeOverrides.Add(new OneTimeAssignment { Date = occDate, Slot = occSlot, RoutineId = fork.Id });
            _memberData.Schedule.UpdatedAt = now;
            Reminder.ApplyTo(_memberData, fork.Id);
            await _repo.SaveMemberDataAsync(_accountId, _memberId, _memberData);
            await _reminders.RescheduleAllAsync(_memberData, FindRoutineName);

            await Shell.Current!.GoToAsync("//home");
            return;
        }

        // Same shadow-the-source naming rule as the Copy to Mine path above, applied
        // to the other way a manufacturer routine gets forked: editing it directly
        // from Home. Since this fork doesn't exist yet, there's no `existing` to
        // carry a snapshot — the manufacturer original itself is the baseline.
        Guid? manufacturerForkSourceId = null;
        string? manufacturerForkSourceName = null;
        List<string>? manufacturerForkSourceKeys = null;

        if (_isManufacturerFork)
        {
            // Move every one of this member's own schedule references from the
            // manufacturer routine's id to a fresh private id — ApplyCellsToSchedule
            // below re-adds _routineId per the (unchanged) DayCells selection, so
            // this only needs to strip the OLD id out from under it. (The
            // just-this-occurrence branch above already forks independently and
            // never reaches here, so this can't double up with it.)
            var oldId = _routineId;
            var manufacturerSource = _manufacturer.Routines.FirstOrDefault(r => r.Id == oldId);
            if (manufacturerSource is not null)
            {
                var sourceKeys = RoutineNamingHelper.ExerciseKeysFor(RoutineType.Hiit, Enumerable.Empty<Guid>(), manufacturerSource.Sections);
                if (RoutineNamingHelper.HasDivergedFromSource(sourceKeys, currentKeys))
                {
                    if (string.Equals(trimmedName, manufacturerSource.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        trimmedName = RoutineNamingHelper.ApplyOwnerPrefix(_memberName, trimmedName);
                        RoutineName = trimmedName;
                    }
                }
                else
                {
                    manufacturerForkSourceId = manufacturerSource.Id;
                    manufacturerForkSourceName = manufacturerSource.OwnerNameSnapshot ?? manufacturerSource.Name;
                    manufacturerForkSourceKeys = sourceKeys;
                }
            }

            _routineId = Guid.NewGuid();
            foreach (var daySlots in _memberData.Schedule.Days.Values)
            {
                daySlots.Am.Remove(oldId);
                daySlots.Pm.Remove(oldId);
            }
            foreach (var oneTime in _memberData.Schedule.OneTimeOverrides.Where(o => o.RoutineId == oldId))
                oneTime.RoutineId = _routineId;
        }

        var routine = existing ?? new RoutineDefinition
        {
            Id = _routineId, AccountId = _accountId, OwnerMemberId = _memberId, OwnerNameSnapshot = _memberName,
            SourceRoutineId = manufacturerForkSourceId, SourceNameSnapshot = manufacturerForkSourceName, SourceExerciseKeysSnapshot = manufacturerForkSourceKeys,
        };
        routine.Name = RoutineName.Trim();
        routine.Visibility = IsAccountShared ? Visibility.Account : Visibility.Private;
        routine.Type = RoutineType.Hiit;
        routine.CycleRepeats = Cycles;
        routine.RestBetweenCyclesSeconds = RestBetweenCycles;
        routine.Sections = BuildSections();
        routine.UpdatedAt = now;

        var isNew = existing is null;
        if (isNew) _shared.Routines.Add(routine);
        await _repo.SaveSharedLibraryAsync(_accountId, _shared);

        RoutineDayScheduleHelper.ApplyCellsToSchedule(_memberData.Schedule, _routineId, DayCells);
        Reminder.ApplyTo(_memberData, _routineId);
        await _repo.SaveMemberDataAsync(_accountId, _memberId, _memberData);
        await _reminders.RescheduleAllAsync(_memberData, FindRoutineName);

        // Lets HomeViewModel offer "add this to today's schedule?" once we're back
        // there — see IHomeWorkoutBridge. Only for a genuinely new routine.
        if (_fromHome && isNew) _homeWorkoutBridge.SetPendingRoutineId(routine.Id);

        await Shell.Current!.GoToAsync(_occDate is not null ? "//home" : "..");
    }

    private string? FindRoutineName(Guid routineId) =>
        _shared.Routines.FirstOrDefault(r => r.Id == routineId)?.Name
        ?? _manufacturer.Routines.FirstOrDefault(r => r.Id == routineId)?.Name;

    /// <summary>Appends "(1)", "(2)", ... to baseName — tries each until one isn't already taken.</summary>
    private static string SuggestUniqueName(string baseName, IReadOnlyCollection<RoutineDefinition> others)
    {
        var taken = others.Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i <= 999; i++)
        {
            var numbered = $"{baseName}({i})";
            if (!taken.Contains(numbered)) return numbered;
        }
        return $"{baseName} ({Guid.NewGuid().ToString()[..4]})";
    }

    [RelayCommand]
    private async Task Cancel() => await Shell.Current.GoToAsync("..");
}

public partial class HiitSectionRowViewModel : ObservableObject
{
    public string Type { get; }
    public string Title { get; }
    public string Description { get; }
    public int Seconds { get; }
    public IRelayCommand<HiitSectionRowViewModel> RemoveCommand { get; }

    /// <summary>The standard warm-up/exercise/rest/cool-down palette plus a couple of
    /// extras — tapping a swatch in the builder calls SelectColorCommand below.</summary>
    public string[] ColorPalette => HiitSectionColors.Palette;

    [ObservableProperty] public partial string Color { get; set; }

    public HiitSectionRowViewModel(string type, string title, string description, int seconds, string color,
        IRelayCommand<HiitSectionRowViewModel> removeCommand)
    {
        Type = type;
        Title = title;
        Description = description;
        Seconds = seconds;
        Color = color;
        RemoveCommand = removeCommand;
    }

    [RelayCommand]
    private void SelectColor(string hex) => Color = hex;
}
