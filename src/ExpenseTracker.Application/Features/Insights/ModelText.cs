using System.Text.RegularExpressions;
using ExpenseTracker.Application.Common.Text;

namespace ExpenseTracker.Application.Features.Insights;

/// <summary>Checks on the free text an agent writes (headlines, tips, reasons) before anyone reads it.</summary>
internal static partial class ModelText
{
    /// <summary>
    /// Usable means: not empty, no digits (every number users read comes from code) and nothing that looks like a link,
    /// an e-mail address or a handle, which prompt-injected user data could try to slip into a text all members see.
    /// </summary>
    public static bool IsUsable(string? text) =>
        !string.IsNullOrWhiteSpace(text) && !TurkishText.HasDigits(text) && !LinkLike().IsMatch(text);

    public static string Clip(string text, int max)
    {
        var trimmed = text.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..(max - 1)] + "…";
    }

    [GeneratedRegex(@"https?:|www\.|@|\.(com|net|org|io|tr|xyz)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LinkLike();
}
