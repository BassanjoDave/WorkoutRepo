using WorkoutTracker.Models;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.Views;

public partial class AppTabBar : ContentView
{
    public static readonly BindableProperty ActiveRouteProperty = BindableProperty.Create(
        nameof(ActiveRoute), typeof(string), typeof(AppTabBar), string.Empty,
        propertyChanged: (bindable, _, _) => ((AppTabBar)bindable).UpdateHighlight());

    public string ActiveRoute
    {
        get => (string)GetValue(ActiveRouteProperty);
        set => SetValue(ActiveRouteProperty, value);
    }

    private readonly IActiveSessionService _session;
    private readonly IWorkoutRepository _repo;

    public AppTabBar(IActiveSessionService session, IWorkoutRepository repo)
    {
        InitializeComponent();
        _session = session;
        _repo = repo;
    }

    /// <summary>Refreshes the bell's unread badge and the avatar — called from each
    /// host page's OnAppearing, same as any page refreshing its own data on
    /// reappearing (there's no active-member-changed event to hook instead).</summary>
    public async Task LoadAsync()
    {
        var account = _session.ActiveAccount;
        var member = _session.ActiveMember;
        if (account is null || member is null) return;

        AvatarBorder.BackgroundColor = Color.FromArgb(member.AvatarColor);
        AvatarInitialLabel.Text = member.Initial;
        var usesPhoto = member.AvatarDisplay == AvatarDisplay.Photo && member.AvatarPhotoBlobFileName is not null;
        AvatarInitialLabel.IsVisible = !usesPhoto;
        AvatarPhotoImage.IsVisible = usesPhoto;
        if (usesPhoto)
        {
            var blobFileName = member.AvatarPhotoBlobFileName!;
            AvatarPhotoImage.Source = ImageSource.FromStream(async _ =>
            {
                var bytes = await _repo.GetProgressPhotoBlobAsync(account.Id, member.Id, blobFileName);
                return bytes is null ? null : new MemoryStream(bytes);
            });
        }

        var memberData = await _repo.GetMemberDataAsync(account.Id, member.Id);
        var unread = memberData.Notifications.Count(n => !n.IsRead);
        UnreadBadge.IsVisible = unread > 0;
        UnreadBadgeLabel.Text = unread.ToString();
    }

    private async void OnBellTapped(object? sender, EventArgs e) => await Shell.Current.GoToAsync("notifications");

    private async void OnAvatarTapped(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//profile");

    private void UpdateHighlight()
    {
        var accent = (Color)Application.Current!.Resources["ColorAccent"];
        var unselected = (Color)Application.Current!.Resources["ColorNeutral500"];
        var divider = (Color)Application.Current!.Resources["ColorDivider"];

        foreach (var button in new[] { HomeButton, ExerciseButton, NutritionButton, HistoryButton, MeasurementsButton, StacksButton, ProfileButton })
        {
            var isActive = (string)button.CommandParameter == ActiveRoute;
            // Outline + text only, no fill — matches the selected-chip look used
            // elsewhere (e.g. ToggleChip), which stays outlined rather than solid
            // so it doesn't compete visually with the dark-theme shadow glow.
            button.TextColor = isActive ? accent : unselected;
            button.FontAttributes = isActive ? FontAttributes.Bold : FontAttributes.None;
            button.BorderColor = isActive ? accent : divider;
        }
    }

    private async void OnTabClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string route } || route == ActiveRoute) return;
        await Shell.Current.GoToAsync($"//{route}");
    }
}
