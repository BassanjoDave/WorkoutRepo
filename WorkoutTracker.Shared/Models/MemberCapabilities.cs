namespace WorkoutTracker.Models;

/// <summary>
/// Governs actions that touch another member's data or the account itself.
/// Viewing/logging/scheduling your own data is never gated by this set.
/// </summary>
public class MemberCapabilities
{
    public bool ViewOthersHistory { get; set; }
    public bool CreateSharedWorkouts { get; set; }
    public bool EditOthersWorkouts { get; set; }
    public bool EditOwnSchedule { get; set; }
    public bool EditAnyoneSchedule { get; set; }
    public bool DeleteContent { get; set; }
    public bool ManageMembers { get; set; }
    public bool ManageBilling { get; set; }
    public bool ExportData { get; set; }

    public static MemberCapabilities ForPreset(RolePreset preset) => preset switch
    {
        RolePreset.Owner => new MemberCapabilities
        {
            ViewOthersHistory = true, CreateSharedWorkouts = true, EditOthersWorkouts = true,
            EditOwnSchedule = true, EditAnyoneSchedule = true, DeleteContent = true,
            ManageMembers = true, ManageBilling = true, ExportData = true,
        },
        // DeleteContent here means "delete content someone else owns" — deleting
        // your own content is baseline and never gated by this set, so Adult
        // (own-only deletion) leaves the flag false; only Owner needs it true.
        RolePreset.Adult => new MemberCapabilities
        {
            ViewOthersHistory = true, CreateSharedWorkouts = true, EditOthersWorkouts = false,
            EditOwnSchedule = true, EditAnyoneSchedule = false, DeleteContent = false,
            ManageMembers = false, ManageBilling = false, ExportData = true,
        },
        RolePreset.Teen => new MemberCapabilities
        {
            ViewOthersHistory = false, CreateSharedWorkouts = true, EditOthersWorkouts = false,
            EditOwnSchedule = true, EditAnyoneSchedule = false, DeleteContent = false,
            ManageMembers = false, ManageBilling = false, ExportData = false,
        },
        RolePreset.Child => new MemberCapabilities(),
        RolePreset.Guest => new MemberCapabilities { EditOwnSchedule = true },
        _ => new MemberCapabilities(),
    };

    /// <summary>
    /// Requires the mandatory device-auth gate: a member holding any of these
    /// must have DeviceAuthMode != None (see Member.RequiresDeviceAuthGate).
    /// </summary>
    public bool RequiresMandatoryGate => ManageMembers || ManageBilling || DeleteContent;
}
