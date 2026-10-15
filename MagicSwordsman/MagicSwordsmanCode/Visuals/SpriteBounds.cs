using Godot;

namespace MagicSwordsman.MagicSwordsmanCode.Visuals;

/// <summary>
/// Presentation only: where the opaque pixels of a texture are, so hover boxes and speech-bubble anchors hug the drawn
/// art instead of the (much larger, transparent-padded) texture rectangle. Results are cached per texture path and
/// computed on a downscaled copy (about 128 px tall), so the first lookup costs one small image read. Every entry
/// point swallows errors and falls back to the full texture rectangle.
/// Coordinates are in texture pixels relative to the texture centre (= the local space of a centred Sprite2D before
/// its scale), so callers multiply by the sprite's scale.
/// </summary>
internal static class SpriteBounds
{
    private const int SampleHeight = 128;
    private const int AlphaThreshold = 48; // 0..255; soft glow edges below this do not count as "the sword"

    private sealed record Info(Rect2 Used, Rect2[] Bands);

    private static readonly Dictionary<string, Info> Cache = new();

    /// <summary>Bounding box of the opaque pixels (centred texture space).</summary>
    public static Rect2 Used(Texture2D tex) => Get(tex).Used;

    /// <summary>
    /// The opaque area cut into <c>bandCount</c> horizontal slices (top to bottom), each slice narrowed to its own opaque
    /// columns; empty slices are left out. A thin blade with a wide guard gives a narrow box for the blade and a wide
    /// one for the guard instead of one wide box.
    /// </summary>
    public static Rect2[] Bands(Texture2D tex) => Get(tex).Bands;

    private static Info Get(Texture2D tex)
    {
        var key = string.IsNullOrEmpty(tex.ResourcePath) ? $"rid:{tex.GetRid().Id}" : tex.ResourcePath;
        if (Cache.TryGetValue(key, out var info)) return info;
        info = Compute(tex);
        Cache[key] = info;
        return info;
    }

    private static Info Compute(Texture2D tex)
    {
        var w = tex.GetWidth();
        var h = tex.GetHeight();
        var full = new Rect2(-w / 2f, -h / 2f, w, h);
        try
        {
            var img = tex.GetImage();
            if (img == null || img.IsEmpty()) return new Info(full, [full]);
            img = (Image)img.Duplicate();
            if (img.IsCompressed()) img.Decompress();
            var sh = Math.Min(SampleHeight, h);
            var sw = Math.Max(1, (int)Math.Round(w * (sh / (float)h)));
            img.Resize(sw, sh, Image.Interpolation.Bilinear);
            img.Convert(Image.Format.Rgba8);
            var data = img.GetData();
            var k = w / (float)sw; // sample px -> texture px (same factor vertically)
            var kh = h / (float)sh;

            const int bandCount = 8;
            var bands = new List<Rect2>();
            int minX = int.MaxValue, maxX = -1, minY = int.MaxValue, maxY = -1;
            for (var b = 0; b < bandCount; b++)
            {
                int y0 = b * sh / bandCount, y1 = (b + 1) * sh / bandCount;
                int bx0 = int.MaxValue, bx1 = -1, by0 = int.MaxValue, by1 = -1;
                for (var y = y0; y < y1; y++)
                for (var x = 0; x < sw; x++)
                {
                    if (data[(y * sw + x) * 4 + 3] < AlphaThreshold) continue;
                    if (x < bx0) bx0 = x;
                    if (x > bx1) bx1 = x;
                    if (y < by0) by0 = y;
                    if (y > by1) by1 = y;
                }

                if (bx1 < 0) continue;
                minX = Math.Min(minX, bx0);
                maxX = Math.Max(maxX, bx1);
                minY = Math.Min(minY, by0);
                maxY = Math.Max(maxY, by1);
                bands.Add(new Rect2(bx0 * k - w / 2f, by0 * kh - h / 2f, (bx1 - bx0 + 1) * k, (by1 - by0 + 1) * kh));
            }

            if (maxX < 0) return new Info(full, [full]);
            var used = new Rect2(minX * k - w / 2f, minY * kh - h / 2f, (maxX - minX + 1) * k, (maxY - minY + 1) * kh);
            return new Info(used, bands.ToArray());
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SpriteBounds] {tex.ResourcePath}: {e.Message}");
            return new Info(full, [full]);
        }
    }

    /// <summary>Axis-aligned global box of a local rectangle mapped through <paramref name="xf"/> (rotation-safe).</summary>
    public static Rect2 GlobalBox(Transform2D xf, Rect2 local)
    {
        var a = xf * local.Position;
        var b = xf * new Vector2(local.End.X, local.Position.Y);
        var c = xf * local.End;
        var d = xf * new Vector2(local.Position.X, local.End.Y);
        var min = new Vector2(Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, d.X)), Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y)));
        var max = new Vector2(Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, d.X)), Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y)));
        return new Rect2(min, max - min);
    }
}
