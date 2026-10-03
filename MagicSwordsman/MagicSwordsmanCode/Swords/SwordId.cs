namespace MagicSwordsman.MagicSwordsmanCode.Swords;

/// <summary>
/// Every magic sword in the mod. The numeric values are written into run saves
/// (Mangeomchong stores them as int[]), so NEVER renumber or reuse a value.
/// Add new swords at the end with a new explicit number.
/// </summary>
public enum SwordId
{
    Gram = 0,           // 그람 (starting sword)
    Ganjiang = 1,       // 간장 (male blade of the pair; acquired together with Moye)
    Moye = 2,           // 막야 (female blade of the pair)
    Kusanagi = 3,       // 쿠사나기
    Tyrfing = 4,        // 티르빙
    Dainsleif = 5,      // 다인슬레이프
    Durandal = 6,       // 뒤랑달
    Skofnung = 7,       // 스코프눙
    Onimaru = 8,        // 오니마루
    ClaiomhSolais = 9,  // 클라이브 솔라시
    Caladbolg = 10,     // 칼라드볼그
}

/// <summary>Why a sword is becoming the current sword. Passed to <see cref="SwordBehavior.CanBecomeCurrent"/>.</summary>
public enum SwitchReason
{
    /// <summary>A card belonging to the sword was played (spec §3 "C 방식").</summary>
    CardPlayed,

    /// <summary>The common "검 바꾸기" (SwordSwap) card or another explicit switch effect.</summary>
    SwapEffect,

    /// <summary>Anything else (relics, powers, events inside combat...).</summary>
    Other,
}
