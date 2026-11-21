using System.Text.RegularExpressions;
namespace Api.Services;

public static class PhoneNumber
{
    /// Returns the number as +E.164 (for example +94771234567), or null if it is not a plausible phone number.
    /// Sri Lankan local numbers (077 123 4567, 0771234567, 771234567) become +94...
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = new string(raw.Where(c => char.IsDigit(c) || c == '+').ToArray());
        if (s.StartsWith("00")) s = "+" + s[2..];
        if (s.StartsWith("0") && s.Length == 10) s = "+94" + s[1..];
        else if (s.StartsWith("94") && s.Length == 11) s = "+" + s;
        else if (s.StartsWith("7") && s.Length == 9) s = "+94" + s;
        return Regex.IsMatch(s, @"^\+\d{9,15}$") ? s : null;
    }
}
