using System.Text.RegularExpressions;
namespace Application.Strategies;

// ---------- demand forecasting (statistical baseline: linear trend over recent daily counts) ----------
public record ForecastPoint(int DayOffset, int Expected);
public record ForecastResult(IReadOnlyList<ForecastPoint> Points, double TrendPerDay, string Confidence);

public static class DemandForecaster
{
    public static ForecastResult Forecast(IReadOnlyList<int> daily, int horizon)
    {
        horizon = Math.Clamp(horizon, 1, 14);
        if (daily.Count == 0)
            return new ForecastResult(Enumerable.Range(1, horizon).Select(i => new ForecastPoint(i, 0)).ToList(), 0, "none");

        var n = Math.Min(daily.Count, 14);
        var recent = daily.Skip(daily.Count - n).ToArray();
        double xm = (n - 1) / 2.0, ym = recent.Average(), num = 0, den = 0;
        for (int i = 0; i < n; i++) { num += (i - xm) * (recent[i] - ym); den += (i - xm) * (i - xm); }
        double slope = den == 0 ? 0 : num / den, intercept = ym - slope * xm;

        var points = Enumerable.Range(1, horizon)
            .Select(d => new ForecastPoint(d, Math.Max(0, (int)Math.Round(intercept + slope * (n - 1 + d))))).ToList();
        return new ForecastResult(points, Math.Round(slope, 2), n >= 10 ? "medium" : n >= 5 ? "low" : "very low");
    }
}

// ---------- suspicious / anomalous request detection (explainable rules, not a black box) ----------
public record AnomalyInput(int PeopleAffected, string Description, int UserRequestsLastHour, int NearbyRequestsLastHour, int SameTextCount);
public record AnomalyResult(double Score, IReadOnlyList<string> Reasons) { public bool IsSuspicious => Score >= 0.6; }

public static class AnomalyDetector
{
    public static AnomalyResult Evaluate(AnomalyInput i)
    {
        double score = 0; var why = new List<string>();
        if (i.PeopleAffected >= 500) { score += 0.4; why.Add("Unusually large number of people affected"); }
        else if (i.PeopleAffected >= 200) { score += 0.25; why.Add("Large number of people affected"); }
        if (i.UserRequestsLastHour >= 5) { score += 0.5; why.Add("Many requests from the same account in one hour"); }
        else if (i.UserRequestsLastHour >= 3) { score += 0.25; why.Add("Several requests from the same account in one hour"); }
        if (i.NearbyRequestsLastHour >= 10) { score += 0.2; why.Add("Burst of requests from one small area"); }
        if (i.SameTextCount >= 3) { score += 0.4; why.Add("Identical text submitted repeatedly"); }
        var d = i.Description ?? "";
        if (d.Trim().Length < 15) { score += 0.15; why.Add("Very short description"); }
        if (d.Length > 20 && d.Count(char.IsUpper) > 0.7 * d.Count(char.IsLetter)) { score += 0.1; why.Add("Mostly capital letters"); }
        if (d.Contains("http:", StringComparison.OrdinalIgnoreCase) || d.Contains("https:", StringComparison.OrdinalIgnoreCase)) { score += 0.3; why.Add("Contains a web link"); }
        return new AnomalyResult(Math.Min(1.0, Math.Round(score, 2)), why);
    }
}

// ---------- input sanitisation ----------
public static class TextSanitizer
{
    /// Strips HTML tags and control characters, collapses spaces and enforces a maximum length.
    /// (Output encoding by the client is still required; this removes the obvious payloads at the door.)
    public static string Clean(string? input, int maxLength = 2000)
    {
        if (string.IsNullOrEmpty(input)) return "";
        var s = Regex.Replace(input, "<[^>]*>", " ");
        s = new string(s.Where(c => !char.IsControl(c) || c == '\n' || c == '\t').ToArray());
        s = Regex.Replace(s, @"[ \t]{2,}", " ").Trim();
        return s.Length > maxLength ? s[..maxLength] : s;
    }
}

// ---------- multilingual notification templates (English / Sinhala / Tamil) ----------
// NOTE: have a native speaker review these before real use. "{ref}" is replaced with the request reference.
public static class Localizer
{
    private static readonly Dictionary<string, (string Subject, string Body)> T = new()
    {
        ["REQUEST_RECEIVED|si"]   = ("ඉල්ලීම {ref} ලැබුණා", "ඔබගේ ඉල්ලීම අපට ලැබුණා. අපගේ කණ්ඩායම එය පරීක්ෂා කරමින් සිටී."),
        ["REQUEST_VERIFIED|si"]   = ("ඔබගේ ඉල්ලීම තහවුරු කළා", "ඔබගේ ඉල්ලීම {ref} තහවුරු කර ඇති අතර එය සකස් කරමින් පවතී."),
        ["SHELTER_ALLOCATED|si"]  = ("නවාතැනක් ලබා දී ඇත", "ඔබට නවාතැනක් වෙන් කර ඇත. විස්තර සඳහා යෙදුම බලන්න."),
        ["RESOURCE_ALLOCATED|si"] = ("උදව් පැමිණෙමින් පවතී", "ඔබගේ ඉල්ලීම සඳහා සැපයුම් වෙන් කර ඇත."),
        ["RESOURCE_DELIVERED|si"] = ("සැපයුම් භාර දී ඇත", "ඔබගේ සැපයුම් භාර දී ඇත."),
        ["REQUEST_RECEIVED|ta"]   = ("கோரிக்கை {ref} பெறப்பட்டது", "உங்கள் கோரிக்கை எங்களுக்கு கிடைத்தது. எங்கள் குழு அதை பரிசீலித்து வருகிறது."),
        ["REQUEST_VERIFIED|ta"]   = ("உங்கள் கோரிக்கை உறுதிசெய்யப்பட்டது", "உங்கள் கோரிக்கை {ref} உறுதிசெய்யப்பட்டு ஏற்பாடு செய்யப்பட்டு வருகிறது."),
        ["SHELTER_ALLOCATED|ta"]  = ("தங்குமிடம் ஒதுக்கப்பட்டது", "உங்களுக்கு தங்குமிடம் ஒதுக்கப்பட்டுள்ளது. விவரங்களை பயன்பாட்டில் பார்க்கவும்."),
        ["RESOURCE_ALLOCATED|ta"] = ("உதவி வந்துகொண்டிருக்கிறது", "உங்கள் கோரிக்கைக்கான பொருட்கள் ஒதுக்கப்பட்டுள்ளன."),
        ["RESOURCE_DELIVERED|ta"] = ("பொருட்கள் வழங்கப்பட்டன", "உங்கள் பொருட்கள் வழங்கப்பட்டுவிட்டன."),
    };

    public static (string Subject, string Body)? TryGet(string template, string? language) =>
        language is "si" or "ta" && T.TryGetValue($"{template}|{language}", out var v) ? v : null;
}
