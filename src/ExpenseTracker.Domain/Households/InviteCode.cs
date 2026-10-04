using System.Security.Cryptography;

namespace ExpenseTracker.Domain.Households;

public static class InviteCode
{
    public const int Length = 8;

    // No 0/O, 1/I/L: codes are read aloud and typed on phones.
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    /// <summary>31^8 ≈ 8.5 × 10^11 codes from a cryptographic RNG, so codes cannot be guessed or predicted.</summary>
    public static string New() => RandomNumberGenerator.GetString(Alphabet, Length);

    public static string Normalize(string? code) =>
        new((code ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    public static bool IsWellFormed(string? code)
    {
        var normalized = Normalize(code);
        return normalized.Length == Length && normalized.All(Alphabet.Contains);
    }
}
