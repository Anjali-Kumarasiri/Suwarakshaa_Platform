namespace Infrastructure.External;

/// Built-in guidance used when the AI service is not running, so the citizen assistant always answers
/// (English, Sinhala, Tamil). Guidance only: it never promises help and gives no medical advice.
public static class LocalFaq
{
    public static string Answer(string message, string? lang)
    {
        var t = (message ?? "").ToLowerInvariant();
        var topic = t.Contains("shelter") || t.Contains("stay") || t.Contains("නවාතැන") || t.Contains("தங்கும") ? "shelter"
                  : t.Contains("status") || t.Contains("track") || t.Contains("තත්ත්ව") || t.Contains("நிலை") ? "status"
                  : t.Contains("donat") || t.Contains("පරිත්‍යාග") || t.Contains("நன்கொடை") ? "donate"
                  : t.Contains("medic") || t.Contains("doctor") || t.Contains("hurt") || t.Contains("injur") ? "medical"
                  : "default";
        return (lang, topic) switch
        {
            ("si", "shelter") => "ආරක්ෂිත නවාතැන් 'Shelters map' පිටුවේ බලන්න. නවාතැනක් අවශ්‍ය නම් 'Request help' හි Shelter තෝරා ඉල්ලීමක් යවන්න.",
            ("si", "status") => "'My requests' පිටුවේ ඔබගේ ඉල්ලීමේ තත්ත්වය බලන්න. තත්ත්වය වෙනස් වූ විට දැනුම්දීමක් ලැබේ.",
            ("si", "donate") => "'Donate' පිටුවට ගොස් ව්‍යාපෘතියක් තෝරා මුදල ඇතුළත් කරන්න. එක් එක් ව්‍යාපෘතියේ මුදල් භාවිත කරන ආකාරය එහි පෙනේ.",
            ("si", "medical") => "කවුරුහරි අන්තරායක තත්ත්වයක සිටී නම් වහාම ඔබේ ප්‍රදේශයේ හදිසි අංකයට අමතන්න. ඉන්පසු මෙහි Medicine ඉල්ලීමක් යවන්න.",
            ("si", _) => "උදව් ඉල්ලීමට 'Request help' විවෘත කර ඔබට අවශ්‍ය දේ තෝරා තත්ත්වය විස්තර කරන්න. කණ්ඩායමක් සෑම ඉල්ලීමක්ම සමාලෝචනය කරයි. ක්ෂණික අන්තරායක සිටී නම් මුලින්ම ඔබේ ප්‍රදේශයේ හදිසි අංකයට අමතන්න.",
            ("ta", "shelter") => "அங்கீகரிக்கப்பட்ட தங்குமிடங்களை 'Shelters map' பக்கத்தில் காணலாம். தங்க இடம் தேவைப்பட்டால் 'Request help' இல் Shelter தேர்ந்தெடுத்து கோரிக்கை அனுப்பவும்.",
            ("ta", "status") => "'My requests' பக்கத்தில் உங்கள் கோரிக்கையின் நிலையைப் பார்க்கலாம். நிலை மாறும்போது அறிவிப்பும் கிடைக்கும்.",
            ("ta", "donate") => "'Donate' பக்கத்தில் ஒரு பிரச்சாரத்தைத் தேர்ந்தெடுத்து தொகையை உள்ளிடவும். ஒவ்வொரு பிரச்சாரத்திலும் நிதி எவ்வாறு பயன்படுத்தப்படுகிறது என்பதைக் காணலாம்.",
            ("ta", "medical") => "யாராவது ஆபத்தில் இருந்தால் உடனே உங்கள் பகுதியின் அவசர எண்ணை அழைக்கவும். பின்னர் இங்கு Medicine கோரிக்கை அனுப்பலாம்.",
            ("ta", _) => "உதவி கேட்க 'Request help' திறந்து தேவையானதைத் தேர்ந்தெடுத்து நிலைமையை விவரிக்கவும். ஒவ்வொரு கோரிக்கையையும் ஒரு குழு பரிசீலிக்கும். உடனடி ஆபத்தில் இருந்தால் முதலில் உங்கள் பகுதியின் அவசர எண்ணை அழைக்கவும்.",
            (_, "shelter") => "You can see approved shelters on the Shelters map page. If you need a place to stay, submit a request and choose Shelter.",
            (_, "status") => "Open 'My requests' to see the status of your request. You will also get a notification when it changes.",
            (_, "donate") => "Open the Donate page, choose a campaign and enter an amount. Each campaign shows how much was raised, spent and what is left.",
            (_, "medical") => "If someone is in danger, call your local emergency number now. You can also submit a Medicine request here so our team can arrange help.",
            _ => "To ask for help, open 'Request help', choose what you need and describe your situation. A team reviews every request. If you are in immediate danger, call your local emergency number first."
        };
    }
}
