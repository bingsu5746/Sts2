namespace Mumyeong.MumyeongCode.Extensions;

/// <summary>
/// Korean particles (조사) for words that are only known at runtime (figure names, shadow epithets), so the Korean text
/// can say 「협곡의 왕」이 / 나폴레옹과 instead of 이(가) / 과(와). Texts add the result as a separate variable right
/// after the word (e.g. "{Figure}{Ga}"); the English texts simply do not use those variables.
/// </summary>
public static class Korean
{
    /// <summary>
    /// <paramref name="withFinal"/> when the last Hangul syllable of the word has a final consonant (받침), else
    /// <paramref name="withoutFinal"/>. Closing brackets, quotes and spaces at the end are skipped. A word that does
    /// not end in Hangul gets the neutral form "이(가)".
    /// </summary>
    public static string Particle(string word, string withFinal, string withoutFinal)
    {
        for (var i = word.Length - 1; i >= 0; i--)
        {
            var c = word[i];
            if (c is '」' or '』' or ')' or '"' or '\'' or ' ' or ']') continue;
            if (c is < '가' or > '힣') break;
            return (c - 0xAC00) % 28 != 0 ? withFinal : withoutFinal;
        }

        return $"{withFinal}({withoutFinal})";
    }

    /// <summary>이 / 가 (subject).</summary>
    public static string Ga(string word) => Particle(word, "이", "가");

    /// <summary>과 / 와 (with).</summary>
    public static string Wa(string word) => Particle(word, "과", "와");

    /// <summary>을 / 를 (object).</summary>
    public static string Reul(string word) => Particle(word, "을", "를");
}
