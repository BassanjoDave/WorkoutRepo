namespace WorkoutTracker.Models;

/// <summary>
/// The human's login. Created once, used to authenticate a device into an
/// Account. A young Member controlled entirely by a holder may never get one
/// of these — that's the difference between a dependent and an independent
/// member. One Identity can be linked to Members in more than one Account
/// (a teen who is both a family-plan member and their own account's holder).
/// </summary>
public class Identity
{
    public Guid Id { get; set; }
    public string? Email { get; set; }
    public string? AuthProviderRef { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
