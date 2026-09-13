namespace WorkoutTracker.Models;

/// <summary>One in-app notification for a member — see MemberData.Notifications
/// and NotificationWriter.NotifyMemberAsync for how these get written.</summary>
public class NotificationEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public bool IsRead { get; set; }

    /// <summary>Shell route to navigate to when this notification is tapped, if any.</summary>
    public string? DeepLinkRoute { get; set; }
}
