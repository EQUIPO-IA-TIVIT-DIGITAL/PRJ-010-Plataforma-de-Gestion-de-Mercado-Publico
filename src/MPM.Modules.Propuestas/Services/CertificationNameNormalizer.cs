using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MPM.Modules.Propuestas.Services;

public static partial class CertificationNameNormalizer
{
    public static string NormalizeKey(string value)
    {
        var text = RemoveAccents(value).Trim().ToLowerInvariant();
        text = Regex.Replace(text, @"iso\s*/?\s*iec", "iso", RegexOptions.CultureInvariant);
        text = Regex.Replace(text, @"[^a-z0-9]+", " ", RegexOptions.CultureInvariant).Trim();
        text = Regex.Replace(text, @"\s+", " ", RegexOptions.CultureInvariant);

        // Códigos aislados son una forma frecuente del mismo certificado ISO.
        if (OnlyCodeRegex().IsMatch(text)) text = "iso " + text;
        return text;
    }

    public static string NormalizeDisplay(string value)
    {
        var trimmed = Regex.Replace(value.Trim(), @"\s+", " ", RegexOptions.CultureInvariant);
        var key = NormalizeKey(trimmed);
        if (key.StartsWith("iso ", StringComparison.Ordinal))
            return "ISO " + key[4..].ToUpperInvariant();
        return trimmed;
    }

    public static IReadOnlySet<string> Tokens(string value)
        => NormalizeKey(value).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);

    private static string RemoveAccents(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    [GeneratedRegex(@"^[0-9]+(?: [0-9]+)?$")]
    private static partial Regex OnlyCodeRegex();
}
