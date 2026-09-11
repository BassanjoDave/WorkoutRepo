using System.Security.Cryptography;
using WorkoutTracker.Models;

namespace WorkoutTracker.Services;

/// <summary>
/// Salts and hashes a member's PIN — see Member.PinHash's own "salted hash
/// only, never store or transmit the PIN itself" doc comment. Not platform-
/// conditional: this is pure System.Security.Cryptography, no native APIs,
/// same reasoning as IProgressPhotoCaptureService not needing #if branches.
/// </summary>
public interface IMemberPinService
{
    /// <summary>Sets member.PinHash. Caller is responsible for persisting the AccountIndex.</summary>
    void SetPin(Member member, string pin);

    bool VerifyPin(Member member, string pin);

    /// <summary>Clears PinHash and resets DeviceAuthMode to None. Caller persists.</summary>
    void ClearPin(Member member);
}

public class MemberPinService : IMemberPinService
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;

    public void SetPin(Member member, string pin)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        member.PinHash = $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}.{Iterations}";
    }

    public bool VerifyPin(Member member, string pin)
    {
        if (string.IsNullOrEmpty(member.PinHash)) return false;
        var parts = member.PinHash.Split('.');
        if (parts.Length != 3) return false;

        try
        {
            var salt = Convert.FromBase64String(parts[0]);
            var expectedHash = Convert.FromBase64String(parts[1]);
            var iterations = int.Parse(parts[2]);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch (FormatException) { return false; }
    }

    public void ClearPin(Member member)
    {
        member.PinHash = null;
        member.DeviceAuthMode = DeviceAuthMode.None;
    }
}
