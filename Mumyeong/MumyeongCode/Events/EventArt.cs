using Godot;
using Mumyeong.MumyeongCode.Figures;
using Mumyeong.MumyeongCode.Visuals;
using MegaCrit.Sts2.Core.Helpers;

namespace Mumyeong.MumyeongCode.Events;

/// <summary>
/// Event background paths of the 황천 영사실 / stage-up events (user request 2026-10-24: a background for every scene and
/// a figure-themed one). Drawn full-screen by <see cref="Cinema.CinemaScreen"/>, which also plays the figure cinematics.
///
/// Background slots (1920x1080 png, shown full-screen, cover-scaled; docs/mumyeong-art-slots.md):
///   images/events/theater_hall.png            the projection room (start pages)
///   images/events/scenes/&lt;scene&gt;.png         one per theater scene (lower-case key, e.g. bridge.png)
///   images/events/figures/&lt;figure&gt;.png       one per figure (descent, contract, card pick, stage-ups)
/// A missing file falls back to the hall, then to the game's own event art.
/// </summary>
public static class EventArt
{
    private const string Dir = $"{MainFile.ResPath}/images/events";

    public static string Hall => $"{Dir}/theater_hall.png";
    public static string Scene(string scene) => $"{Dir}/scenes/{scene.ToLowerInvariant()}.png";
    public static string Figure(FigureId figure) => $"{Dir}/figures/{SpiritVisuals.FileKey(figure)}.png";

    /// <summary>The path if it exists, else the hall, else the game's grave event art.</summary>
    public static string Resolve(string path)
    {
        if (ResourceLoader.Exists(path)) return path;
        return ResourceLoader.Exists(Hall) ? Hall : ImageHelper.GetImagePath("events/grave_of_the_forgotten.png");
    }
}
