using Godot;

namespace MagicSwordsman.MagicSwordsmanCode.Visuals;

/// <summary>Effects of the death to the Architect (SwordVisuals.Architect.cs).</summary>
public static partial class SwordFx
{
    /// <summary>
    /// A blade strikes the unseen wall in front of the Architect and is thrown back: the blades' clash, plus a grey ripple
    /// flattened across the line of the blow (<paramref name="dir"/>) — the wall, felt only where it is struck.
    /// </summary>
    internal static void Repel(Node2D parent, Vector2 pos, Color col, Vector2 dir)
    {
        try
        {
            Clash(parent, pos, col, 1f);
            var fx = Root(parent, pos, 0.6, 3);
            fx.Rotation = dir.Angle();
            Ring(fx, Vector2.Zero, new Color(0.5f, 0.52f, 0.58f), 10, 46, 0.28, squash: 1.9f);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordFx] repel: {e.Message}");
        }
    }

    /// <summary>
    /// The Architect takes a blade: a thin grey thread flashes from it (<paramref name="from"/>) to the blade
    /// (<paramref name="to"/>), and a cold flare on the blade.
    /// </summary>
    internal static void Seize(Node2D parent, Vector2 from, Vector2 to, Color col, double delay = 0)
    {
        try
        {
            var d = to - from;
            var fx = Root(parent, (from + to) / 2, 0.8 + delay, 3);
            Beam(fx, Vector2.Zero, d.Angle() + Mathf.Pi / 2, col, d.Length(), 5, 0.5, delay);
            Flare(fx, d / 2, col, 70, 0.4, delay);
            Ring(fx, d / 2, col, 8, 34, 0.3, delay);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordFx] seize: {e.Message}");
        }
    }
}
