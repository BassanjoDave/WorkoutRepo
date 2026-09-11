using WorkoutTracker.Models;

namespace WorkoutTracker.Services;

/// <summary>
/// The seam the billing layer takes over later. Every member-adding operation
/// (invite generation, invite redemption, direct dependent creation) must
/// call HasCapacity before writing a new Member — never compare
/// Account.SeatLimit inline, so the real entitlement check can replace this
/// implementation without touching call sites.
/// </summary>
public interface ISeatAvailabilityService
{
    bool HasCapacity(AccountIndex account);
}

public class SeatAvailabilityService : ISeatAvailabilityService
{
    public bool HasCapacity(AccountIndex account)
    {
        var activeCount = account.Members.Count(m => m.Status == MemberStatus.Active);
        return activeCount < account.Account.SeatLimit;
    }
}
