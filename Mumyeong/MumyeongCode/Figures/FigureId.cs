namespace Mumyeong.MumyeongCode.Figures;

/// <summary>
/// The great figures whose core 무명 can receive (spec §3). The numbers are written into saves
/// (<see cref="Relics.SoulCore"/>): never renumber, add new figures at the end.
/// </summary>
public enum FigureId
{
    Guanyu = 0,
    Joan = 1,
    Leonidas = 2,
    Genghis = 3,
    YiSunsin = 4,
    Napoleon = 5,
}

/// <summary>Contract made in the 황천 영사실 (spec §4.2–4.3). Saved as int.</summary>
public enum ContractType
{
    None = 0,
    Resonance = 1, // 공명 계약: resonance 3/3
    Grudge = 2,    // 한 계약: resonance 2/3 or less
}

/// <summary>빙의 → 동기화 → 집중 → 강림 (spec §5). Saved as int.</summary>
public enum SoulStage
{
    Possession = 0,
    Synchronization = 1,
    Focus = 2,
    Descent = 3,
}
