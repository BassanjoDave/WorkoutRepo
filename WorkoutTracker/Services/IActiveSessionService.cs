using WorkoutTracker.Models;
using WorkoutTracker.Services.Storage;

namespace WorkoutTracker.Services;

/// <summary>
/// Tracks which Account/Member is active on this device right now, and
/// exposes the current member's effective capabilities to view models. This
/// is what the profile gate sets and what every ViewModel reads to decide
/// what to show — no view model touches DeviceIndex or IWorkoutRepository directly.
/// </summary>
public interface IActiveSessionService
{
    Account? ActiveAccount { get; }
    Member? ActiveMember { get; }
    MemberCapabilities ActiveCapabilities { get; }

    Task LoadLastSessionAsync();
    Task<AccountIndex?> SelectMemberAsync(Guid accountId, Guid memberId);
    Task ClearAsync();
}

public class ActiveSessionService : IActiveSessionService
{
    private readonly IWorkoutRepository _repo;
    private AccountIndex? _accountIndex;

    public ActiveSessionService(IWorkoutRepository repo) => _repo = repo;

    public Account? ActiveAccount => _accountIndex?.Account;
    public Member? ActiveMember { get; private set; }
    public MemberCapabilities ActiveCapabilities =>
        ActiveMember?.EffectiveCapabilities() ?? new MemberCapabilities();

    public async Task LoadLastSessionAsync()
    {
        var deviceIndex = await _repo.GetDeviceIndexAsync();
        if (deviceIndex.ActiveAccountId is Guid accountId && deviceIndex.ActiveMemberId is Guid memberId)
        {
            await SelectMemberAsync(accountId, memberId);
        }
    }

    public async Task<AccountIndex?> SelectMemberAsync(Guid accountId, Guid memberId)
    {
        var accountIndex = await _repo.GetAccountIndexAsync(accountId);
        var member = accountIndex?.Members.FirstOrDefault(m => m.Id == memberId && m.Status == MemberStatus.Active);
        if (accountIndex is null || member is null) return null;

        _accountIndex = accountIndex;
        ActiveMember = member;

        var deviceIndex = await _repo.GetDeviceIndexAsync();
        deviceIndex.ActiveAccountId = accountId;
        deviceIndex.ActiveMemberId = memberId;
        if (!deviceIndex.AccountIds.Contains(accountId)) deviceIndex.AccountIds.Add(accountId);
        await _repo.SaveDeviceIndexAsync(deviceIndex);

        return accountIndex;
    }

    public async Task ClearAsync()
    {
        _accountIndex = null;
        ActiveMember = null;
        var deviceIndex = await _repo.GetDeviceIndexAsync();
        deviceIndex.ActiveAccountId = null;
        deviceIndex.ActiveMemberId = null;
        await _repo.SaveDeviceIndexAsync(deviceIndex);
    }
}
