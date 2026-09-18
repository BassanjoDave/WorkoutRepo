using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Media;
using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.ViewModels;

/// <summary>
/// Flattens a HIIT routine's sections across its configured cycles (with a
/// rest gap between cycles when set), runs a one-second countdown per
/// section, speaks a transition announcement at the start of each, and beeps
/// in the last three seconds. On finish it writes one WorkoutSession — every
/// flattened section becomes an entry, matching the source design's log
/// shape (set index, "Ns" duration, weight "HIIT").
/// </summary>
public partial class HiitPlayerViewModel : ObservableObject, IQueryAttributable, IDisposable
{
    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;
    private readonly IHiitSoundService _sounds;

    private Guid _routineId;
    private TimeOnly _time = new(7, 0);
    private DateOnly _date;
    private Guid _accountId;
    private Guid _memberId;
    private string _routineName = "";
    private List<HiitSection> _flatSections = new();
    private IDispatcherTimer? _timer;
    private HiitSettings _hiitSettings = new();

    [ObservableProperty] public partial string RoutineName { get; set; } = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplaySectionNumber))]
    public partial int CurrentIndex { get; set; }
    /// <summary>1-based for display — "Section 1 of 11" on the first section, not "Section 0 of 11".</summary>
    public int DisplaySectionNumber => CurrentIndex + 1;
    [ObservableProperty] public partial int TotalSections { get; set; }
    [ObservableProperty] public partial string CurrentTitle { get; set; } = "";
    [ObservableProperty] public partial string CurrentDescription { get; set; } = "";
    [ObservableProperty] public partial int RemainingSeconds { get; set; }

    /// <summary>Preview of the section coming up after this one — hidden on the
    /// last section, since there's nothing to preview.</summary>
    [ObservableProperty] public partial bool HasNextSection { get; set; }
    [ObservableProperty] public partial string NextTitle { get; set; } = "";
    [ObservableProperty] public partial Color NextSectionColor { get; set; } = Colors.Transparent;

    /// <summary>The current section's own standard/custom color — restored after each
    /// countdown flash (see FlashAsync) and what DisplayBackgroundColor reverts to.</summary>
    private Color _sectionColor = Colors.Transparent;
    [ObservableProperty] public partial Color DisplayBackgroundColor { get; set; } = Colors.Transparent;
    [ObservableProperty] public partial bool IsRunning { get; set; }
    [ObservableProperty] public partial bool Started { get; set; }
    [ObservableProperty] public partial bool Finished { get; set; }
    [ObservableProperty] public partial bool Muted { get; set; }
    [ObservableProperty] public partial string StartButtonLabel { get; set; } = "Start";

    public HiitPlayerViewModel(IActiveSessionService session, IWorkoutRepository repo, IHiitSoundService sounds)
    {
        _session = session;
        _repo = repo;
        _sounds = sounds;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _routineId = Guid.Parse((string)query["routineId"]);
        _time = query.TryGetValue("time", out var t) ? TimeOnly.ParseExact((string)t, "HH:mm") : new TimeOnly(7, 0);
        _date = query.TryGetValue("date", out var d) ? DateOnly.Parse((string)d) : DateOnly.FromDateTime(DateTime.Today);
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

        var shared = await _repo.GetSharedLibraryAsync(account.Id);
        var manufacturer = await _repo.GetManufacturerLibraryAsync();
        var routine = shared.Routines.FirstOrDefault(r => r.Id == _routineId)
            ?? manufacturer.Routines.FirstOrDefault(r => r.Id == _routineId);
        if (routine?.Sections is null || routine.Sections.Count == 0) return;

        _routineName = routine.Name;
        RoutineName = routine.Name;

        var memberData = await _repo.GetMemberDataAsync(account.Id, member.Id);
        _hiitSettings = memberData.HiitSettings;
        Muted = _hiitSettings.Muted;

        var cycles = Math.Max(1, routine.CycleRepeats ?? 1);
        var rest = routine.RestBetweenCyclesSeconds ?? 0;
        _flatSections = new List<HiitSection>();
        for (var c = 0; c < cycles; c++)
        {
            _flatSections.AddRange(routine.Sections);
            if (c < cycles - 1 && rest > 0)
            {
                _flatSections.Add(new HiitSection { Id = Guid.NewGuid(), Type = "Rest", Title = "Repeat Cycle", Description = "Rest before the next cycle", Seconds = rest, IsCycleRest = true });
            }
        }

        TotalSections = _flatSections.Count;
        CurrentIndex = 0;
        RemainingSeconds = _flatSections[0].Seconds;
        UpdateCurrentLabels();
    }

    private void UpdateCurrentLabels()
    {
        var section = _flatSections[CurrentIndex];
        CurrentTitle = section.Title;
        CurrentDescription = section.Description;
        _sectionColor = Color.FromArgb(HiitSectionColors.Resolve(section));
        DisplayBackgroundColor = _sectionColor;

        var nextIndex = CurrentIndex + 1;
        HasNextSection = nextIndex < _flatSections.Count;
        if (HasNextSection)
        {
            var next = _flatSections[nextIndex];
            NextTitle = next.Title;
            NextSectionColor = Color.FromArgb(HiitSectionColors.Resolve(next));
        }
    }

    private static string Announcement(HiitSection section) =>
        section.IsCycleRest ? $"Repeat Cycle in {section.Seconds} seconds" : $"Begin {section.Title}";

    [RelayCommand]
    private async Task ToggleStart()
    {
        if (Finished) return;
        if (!Started)
        {
            Started = true;
            await SpeakAsync("Start Workout");
            await Task.Delay(1400);
            if (Finished) return; // exited/finished during the delay
            await SpeakAsync(Announcement(_flatSections[0]));
            IsRunning = true;
            StartButtonLabel = "Resume";
            RunTimer();
        }
        else
        {
            IsRunning = true;
            RunTimer();
        }
    }

    [RelayCommand]
    private void Pause()
    {
        IsRunning = false;
        StartButtonLabel = "Resume";
        _timer?.Stop();
    }

    [RelayCommand]
    private void ToggleMute()
    {
        Muted = !Muted;
        _ = PersistMuteAsync();
    }

    private async Task PersistMuteAsync()
    {
        var memberData = await _repo.GetMemberDataAsync(_accountId, _memberId);
        memberData.HiitSettings.Muted = Muted;
        await _repo.SaveMemberDataAsync(_accountId, _memberId, memberData);
    }

    private void RunTimer()
    {
        _timer?.Stop();
        _timer = Application.Current!.Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    private void Tick()
    {
        if (!IsRunning) return;

        RemainingSeconds--;

        // Announced for the NEXT number down, not this one: e.g. the instant the
        // display becomes 4, we say "Three" — that word then has this whole real
        // second (while the display sits at 4) to finish playing before the
        // following tick moves the display to 3. Matching the word to the number
        // already on screen sounds like it's reacting to it a beat late, because
        // TTS has real startup latency and the visual change is instantaneous.
        if (RemainingSeconds is 4 or 3 or 2)
        {
            _sounds.PlayBeep(Muted || !_hiitSettings.EndBeepEnabled);
            // A synthesized tone needs an audio-player dependency heavy enough not to be
            // worth it (see IHiitSoundService); a spoken count works with the TTS engine
            // already wired up for section announcements, no extra asset required.
            if (!Muted && _hiitSettings.EndBeepEnabled)
            {
                _ = SpeakCountAsync(RemainingSeconds switch { 4 => "Three", 3 => "Two", _ => "One" });
            }
        }

        // The displayed countdown itself (3, 2, 1 — not the voice's one-ahead offset
        // above) gets a visual pulse on each second change, independent of mute.
        if (RemainingSeconds is 3 or 2 or 1)
        {
            _ = FlashAsync();
        }

        if (RemainingSeconds > 0) return;

        var nextIndex = CurrentIndex + 1;
        if (nextIndex < _flatSections.Count)
        {
            CurrentIndex = nextIndex;
            RemainingSeconds = _flatSections[nextIndex].Seconds;
            UpdateCurrentLabels();
            _ = SpeakAsync(Announcement(_flatSections[nextIndex]));
            return;
        }

        _timer?.Stop();
        IsRunning = false;
        Finished = true;
        DisplayBackgroundColor = Colors.Transparent;
        _ = SpeakAsync("End of workout");
        _ = SaveSessionAsync();
    }

    /// <summary>Briefly flashes the section's background to white then back — a
    /// visual pulse timed with each of the last three countdown ticks.</summary>
    private async Task FlashAsync()
    {
        DisplayBackgroundColor = Colors.White;
        await Task.Delay(200);
        DisplayBackgroundColor = _sectionColor;
    }

    private async Task SpeakAsync(string text)
    {
        if (Muted || !_hiitSettings.VoiceEnabled) return;
        await SpeakRawAsync(text);
    }

    /// <summary>The 3/2/1 countdown is its own cue (gated by EndBeepEnabled, checked by the caller), independent of the section-announcement voice toggle.</summary>
    private async Task SpeakCountAsync(string text) => await SpeakRawAsync(text);

    private async Task SpeakRawAsync(string text)
    {
        try
        {
            await TextToSpeech.Default.SpeakAsync(text, new SpeechOptions { Volume = _hiitSettings.Volume / 100f });
        }
        catch { /* no TTS engine available on this platform/device — the countdown UI still works */ }
    }

    private async Task SaveSessionAsync()
    {
        // Warm ups, rests, and cool downs are timing scaffolding, not logged
        // work — only the actual exercise/custom intervals go in the log.
        var entries = _flatSections
            .Select((section, i) => (section, i))
            .Where(t => t.section.Type is not ("Warm Up" or "Rest" or "Cool Down"))
            .Select(t => new SessionExerciseEntry
            {
                ExerciseId = Guid.NewGuid(),
                Done = true,
                Label = t.section.Title,
                Groups = { new SessionSetEntry { Sets = (t.i + 1).ToString(), Reps = $"{t.section.Seconds}s", Weight = "HIIT" } },
            }).ToList();

        var memberData = await _repo.GetMemberDataAsync(_accountId, _memberId);
        memberData.Sessions.Add(new WorkoutSession
        {
            Id = Guid.NewGuid(),
            AccountId = _accountId,
            MemberId = _memberId,
            RoutineDefinitionId = _routineId,
            RoutineNameSnapshot = _routineName,
            Date = _date,
            Time = _time,
            Status = SessionStatus.Completed,
            CompletedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Entries = entries,
        });
        await _repo.SaveMemberDataAsync(_accountId, _memberId, memberData);
    }

    [RelayCommand]
    private async Task Exit()
    {
        _timer?.Stop();
        Finished = true; // guards the delayed continuation in ToggleStart if exit happens mid-countdown-in
        await Shell.Current.GoToAsync("..");
    }

    /// <summary>Pauses the countdown and opens the builder for this routine — see StandardBuilderViewModel.Save for the occurrence-scope prompt (this mirrors HiitBuilderViewModel's copy of the same logic).</summary>
    [RelayCommand]
    private async Task EditWorkout()
    {
        IsRunning = false;
        StartButtonLabel = "Resume";
        _timer?.Stop();
        await Shell.Current.GoToAsync($"hiitBuilder?routineId={_routineId}&occDate={_date:yyyy-MM-dd}&occTime={_time:HH\\:mm}");
    }

    public void Dispose() => _timer?.Stop();
}
