namespace WorkoutTracker.Models;

public class Member
{
    public Guid Id { get; set; }

    /// <summary>
    /// The account this member currently belongs to. This is the field a
    /// junior split-off re-points; Id never changes across that migration.
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>Null = no independent login (a dependent controlled by the holder).</summary>
    public Guid? IdentityId { get; set; }

    public string DisplayName { get; set; } = "";
    public string AvatarColor { get; set; } = "";
    public string Initial { get; set; } = "";

    /// <summary>
    /// Filename of this member's uploaded avatar photo, stored via
    /// IWorkoutRepository's progress-photo blob methods — there's no
    /// dedicated "profile photo" storage server-side, just a generic
    /// per-member photo blob keyed by filename, so this reuses it rather
    /// than duplicating a byte-identical endpoint. Null = no photo uploaded.
    /// </summary>
    public string? AvatarPhotoBlobFileName { get; set; }

    /// <summary>Never Photo while AvatarPhotoBlobFileName is null.</summary>
    public AvatarDisplay AvatarDisplay { get; set; } = AvatarDisplay.Initial;
    public DateOnly? DateOfBirth { get; set; }
    public MemberStatus Status { get; set; } = MemberStatus.Active;
    public RolePreset RolePreset { get; set; } = RolePreset.Adult;

    /// <summary>Null = pure preset. Any non-null field on this record overrides the preset.</summary>
    public MemberCapabilityOverrides? Overrides { get; set; }

    /// <summary>
    /// Whether the account's primary holder may additionally view this
    /// member's progress photos, beyond the member themself. Null = role-based
    /// default (see EffectiveProgressPhotosVisibleToHolder). Deliberately a
    /// dedicated field, not folded into MemberCapabilityOverrides — that
    /// record is action-capability RBAC (what a member may DO); this is a
    /// content-privacy setting the primary holder sets per member, orthogonal
    /// to role/capabilities.
    /// </summary>
    public bool? ProgressPhotosVisibleToHolder { get; set; }

    public DeviceAuthMode DeviceAuthMode { get; set; } = DeviceAuthMode.None;

    /// <summary>Salted hash only — never store or transmit the PIN itself.</summary>
    public string? PinHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public MemberCapabilities EffectiveCapabilities()
    {
        var caps = MemberCapabilities.ForPreset(RolePreset);
        return Overrides?.ApplyTo(caps) ?? caps;
    }

    public bool RequiresDeviceAuthGate => EffectiveCapabilities().RequiresMandatoryGate;

    /// <summary>
    /// Resolves the null-means-default rule: Child-role members default to
    /// visible to the holder; every other role defaults to hidden. The
    /// primary holder can flip either default per member (see
    /// ManageMembersViewModel.ToggleProgressPhotoVisibilityAsync). Mirrors the
    /// EffectiveCapabilities() null-coalescing pattern above.
    /// </summary>
    public bool EffectiveProgressPhotosVisibleToHolder =>
        ProgressPhotosVisibleToHolder ?? (RolePreset == RolePreset.Child);
}

/// <summary>Sparse override set — only non-null fields deviate from the role preset.</summary>
public class MemberCapabilityOverrides
{
    public bool? ViewOthersHistory { get; set; }
    public bool? CreateSharedWorkouts { get; set; }
    public bool? EditOthersWorkouts { get; set; }
    public bool? EditOwnSchedule { get; set; }
    public bool? EditAnyoneSchedule { get; set; }
    public bool? DeleteContent { get; set; }
    public bool? ManageMembers { get; set; }
    public bool? ManageBilling { get; set; }
    public bool? ExportData { get; set; }

    public MemberCapabilities ApplyTo(MemberCapabilities baseCaps) => new()
    {
        ViewOthersHistory = ViewOthersHistory ?? baseCaps.ViewOthersHistory,
        CreateSharedWorkouts = CreateSharedWorkouts ?? baseCaps.CreateSharedWorkouts,
        EditOthersWorkouts = EditOthersWorkouts ?? baseCaps.EditOthersWorkouts,
        EditOwnSchedule = EditOwnSchedule ?? baseCaps.EditOwnSchedule,
        EditAnyoneSchedule = EditAnyoneSchedule ?? baseCaps.EditAnyoneSchedule,
        DeleteContent = DeleteContent ?? baseCaps.DeleteContent,
        ManageMembers = ManageMembers ?? baseCaps.ManageMembers,
        ManageBilling = ManageBilling ?? baseCaps.ManageBilling,
        ExportData = ExportData ?? baseCaps.ExportData,
    };
}
