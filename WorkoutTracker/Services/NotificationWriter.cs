using WorkoutTracker.Models;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.Services;

/// <summary>Shared load-mutate-save helper for appending an in-app notification to a
/// member's own MemberData — see MemberData.Notifications. Small and static since it's
/// called from a handful of unrelated ViewModels that already hold an IWorkoutRepository
/// in scope, not something worth promoting to its own injected service.</summary>
public static class NotificationWriter
{
    public static async Task NotifyMemberAsync(IWorkoutRepository repo, Guid accountId, Guid memberId,
        string title, string body, string? deepLinkRoute = null)
    {
        var data = await repo.GetMemberDataAsync(accountId, memberId);
        data.Notifications.Add(new NotificationEntry { Title = title, Body = body, DeepLinkRoute = deepLinkRoute });
        await repo.SaveMemberDataAsync(accountId, memberId, data);
    }
}
