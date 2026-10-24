using Godot;
using MegaCrit.Sts2.Core.Assets;

namespace Mumyeong.MumyeongCode.Visuals;

/// <summary>
/// The character's body scenes, built in code at runtime (2026-10-24). The release .pck is made without the Godot editor
/// (BSchneppe.StS2.PckPacker), which cannot pack .tscn files, so each scene is assembled here, packed into a PackedScene
/// and registered at a res:// path (Resource.TakeOverPath + the game's asset cache). The game and BaseLib then load and
/// convert it exactly like a scene file (BaseLib keys its scene conversion on PackedScene.ResourcePath).
///
///   combat       reaper ("Visuals/Body") + guardian spirit ("Visuals/SpiritRoot/Spirit", hidden until SpiritVisuals
///                sets the figure); clips idle / Attack / Cast / Hit / Dead; the spirit floats on its own player.
///   rest_site    sitting by the fire ("ControlRoot/Visuals"); the spirit sits beside him ("ControlRoot/Spirit").
///   merchant     standing, spirit behind.
///   char_select  the screening hall; the six spirits rise behind him in turn.
/// Art: tools/gen_character.py. Talk anchors in SpiritTalk are local points on these sprites.
/// </summary>
public static class RuntimeScenes
{
    public const string Combat = $"{MainFile.ResPath}/scenes/mumyeong_combat.tscn";
    public const string RestSite = $"{MainFile.ResPath}/scenes/mumyeong_rest_site.tscn";
    public const string Merchant = $"{MainFile.ResPath}/scenes/mumyeong_merchant.tscn";
    public const string CharSelect = $"{MainFile.ResPath}/scenes/mumyeong_char_select_bg.tscn";

    private const string Img = $"{MainFile.ResPath}/images/character";

    private static readonly string[] Figures = ["guanyu", "joan", "leonidas", "genghis", "yisunsin", "napoleon"];

    // layout (scene units); reaper.png 560x860 at 0.5 with the feet at y ~ 0
    private const float BodyScale = 0.5f;
    private static readonly Vector2 BodyPos = new(0, -198);
    private const float SpiritScale = 0.6f;
    private static readonly Vector2 SpiritRootPos = new(-135, -40);
    private static readonly Vector2 SpiritPos = new(0, -283);
    private static readonly Vector2 RestBodyPos = new(120, -203);
    private const float RestBodyScale = 0.72f;
    private static readonly Vector2 RestSpiritPos = new(-50, -222);
    private const float RestSpiritScale = 0.42f;

    private static readonly Dictionary<string, PackedScene> Built = new();

    /// <summary>
    /// The scene's path once it was built and registered, else the fallback (placeholder visuals). A failed build is
    /// retried on the next call (the art may not be loadable yet).
    /// </summary>
    public static string? PathOr(string path, string? fallback)
    {
        if (Built.ContainsKey(path)) return path;
        if (TryBuild(path) is not { } scene) return fallback;
        Built[path] = scene;
        return path;
    }

    private static PackedScene? TryBuild(string path)
    {
        try
        {
            if (!ResourceLoader.Exists($"{Img}/reaper.png"))
            {
                MainFile.Logger.Warn("[RuntimeScenes] character art missing from the pck; using placeholder visuals");
                return null;
            }

            Node root = path switch
            {
                Combat => BuildCombat(),
                RestSite => BuildRestSite(),
                Merchant => BuildMerchant(),
                CharSelect => BuildCharSelect(),
                _ => throw new ArgumentException(path),
            };
            var scene = Pack(root, path);
            root.QueueFree();
            return scene;
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[RuntimeScenes] {path}: {e.Message}");
            return null;
        }
    }

    private static PackedScene Pack(Node root, string path)
    {
        SetOwner(root, root);
        foreach (var unique in root.GetMeta("unique_names", new Godot.Collections.Array()).AsGodotArray())
            root.GetNode(unique.AsString()).UniqueNameInOwner = true;
        root.RemoveMeta("unique_names");
        var scene = new PackedScene();
        var err = scene.Pack(root);
        if (err != Error.Ok) throw new Exception($"pack failed: {err}");
        scene.TakeOverPath(path);
        PreloadManager.Cache.SetAsset(path, scene);
        return scene;
    }

    private static void SetOwner(Node node, Node owner)
    {
        foreach (var child in node.GetChildren())
        {
            child.Owner = owner;
            SetOwner(child, owner);
        }
    }

    private static void MarkUnique(Node root, string relPath)
    {
        var list = root.GetMeta("unique_names", new Godot.Collections.Array()).AsGodotArray();
        list.Add(relPath);
        root.SetMeta("unique_names", list);
    }

    private static Texture2D Tex(string file) => GD.Load<Texture2D>($"{Img}/{file}");

    private static T Add<T>(Node parent, T child, string name) where T : Node
    {
        child.Name = name;
        parent.AddChild(child);
        return child;
    }

    // ------------------------------------------------------------------ scenes

    private static Node BuildCombat()
    {
        var root = new Node2D { Name = "Mumyeong" };
        Add(root, new Control
        {
            Position = new Vector2(-110, -430), Size = new Vector2(220, 430), MouseFilter = Control.MouseFilterEnum.Ignore,
        }, "Bounds");
        // where the game's own talk bubbles point: right edge of the smoke face
        Add(root, new Marker2D { Position = new Vector2(31, -295) }, "TalkPos");
        MarkUnique(root, "TalkPos");
        var visuals = Add(root, new Node2D(), "Visuals");
        MarkUnique(root, "Visuals");
        var spiritRoot = Add(visuals, new Node2D { Position = SpiritRootPos }, "SpiritRoot");
        Add(spiritRoot, HiddenSpirit(SpiritPos, SpiritScale), "Spirit");
        // click the spirit to talk to it (SpiritTalk); Pass so it never eats a click meant for the game
        Add(spiritRoot, new Control
        {
            // the spirit's head, above and left of his hat: kept clear of the body's Bounds so his own hover still works
            Position = new Vector2(-100, -560), Size = new Vector2(160, 120),
            MouseFilter = Control.MouseFilterEnum.Pass, MouseDefaultCursorShape = Control.CursorShape.PointingHand,
        }, "SpiritClick");
        Add(visuals, new Sprite2D { Texture = Tex("reaper.png"), Position = BodyPos, Scale = Vector2.One * BodyScale },
            "Body");

        var idle = Clip("idle", 3.2f, true).Key("Visuals/Body:position", (0, BodyPos), (1.6f, BodyPos + new Vector2(0, -3)),
            (3.2f, BodyPos));
        var attack = Clip("Attack", 0.55f)
            .Key("Visuals:position", (0, Vector2.Zero), (0.12f, new Vector2(-10, 0)), (0.24f, new Vector2(46, 0)),
                (0.55f, Vector2.Zero))
            .Key("Visuals/SpiritRoot:position", (0, SpiritRootPos), (0.14f, SpiritRootPos + new Vector2(-14, -6)),
                (0.26f, SpiritRootPos + new Vector2(70, -4)), (0.55f, SpiritRootPos));
        var cast = Clip("Cast", 0.7f)
            .Key("Visuals:position", (0, Vector2.Zero), (0.25f, new Vector2(0, -10)), (0.7f, Vector2.Zero))
            .Key("Visuals/SpiritRoot:scale", (0, Vector2.One), (0.25f, Vector2.One * 1.06f), (0.7f, Vector2.One));
        var hit = Clip("Hit", 0.45f)
            .Key("Visuals:position", (0, Vector2.Zero), (0.08f, new Vector2(-20, 0)), (0.45f, Vector2.Zero))
            .Key("Visuals:modulate", (0, Colors.White), (0.06f, new Color(1, 0.55f, 0.55f)), (0.3f, Colors.White));
        var dead = Clip("Dead", 2.0f)
            .Key("Visuals:position", (0, Vector2.Zero), (2.0f, new Vector2(0, 24)))
            .Key("Visuals:modulate", (0, Colors.White), (0.4f, new Color(0.8f, 0.8f, 0.9f)), (2.0f, new Color(1, 1, 1, 0)));
        AddPlayer(root, "AnimationPlayer", "idle", idle, attack, cast, hit, dead);
        AddPlayer(root, "SpiritFloat", "float", Float("Visuals/SpiritRoot/Spirit", SpiritPos, 10, 4.6f));
        return root;
    }

    private static Node BuildMerchant()
    {
        var root = new Node2D { Name = "MumyeongMerchant" };
        var spiritRoot = Add(root, new Node2D { Position = SpiritRootPos }, "SpiritRoot");
        Add(spiritRoot, HiddenSpirit(SpiritPos, SpiritScale), "Spirit");
        Add(root, new Sprite2D { Texture = Tex("reaper.png"), Position = BodyPos, Scale = Vector2.One * BodyScale },
            "Visuals");
        MarkUnique(root, "Visuals");
        AddPlayer(root, "AnimationPlayer", "idle",
            Clip("idle", 3.2f, true).Key("Visuals:position", (0, BodyPos), (1.6f, BodyPos + new Vector2(0, -3)),
                (3.2f, BodyPos)));
        AddPlayer(root, "SpiritFloat", "float", Float("SpiritRoot/Spirit", SpiritPos, 10, 4.6f));
        return root;
    }

    private static Node BuildRestSite()
    {
        var root = new Node2D { Name = "MumyeongRestSite" };
        var control = Add(root, new Control { MouseFilter = Control.MouseFilterEnum.Ignore }, "ControlRoot");
        Add(control, new Control { Position = new Vector2(-150, -420), Size = new Vector2(420, 382) }, "Hitbox");
        MarkUnique(root, "ControlRoot/Hitbox");
        Add(control, HiddenSpirit(RestSpiritPos, RestSpiritScale), "Spirit");
        Add(control, new Sprite2D
        {
            Texture = Tex("reaper_rest.png"), Position = RestBodyPos, Scale = Vector2.One * RestBodyScale,
        }, "Visuals");
        MarkUnique(root, "ControlRoot/Visuals");
        AddPlayer(root, "AnimationPlayer", "idle",
            Clip("idle", 3.6f, true).Key("ControlRoot/Visuals:position", (0, RestBodyPos),
                (1.8f, RestBodyPos + new Vector2(0, -2)), (3.6f, RestBodyPos)));
        AddPlayer(root, "SpiritFloat", "float", Float("ControlRoot/Spirit", RestSpiritPos, 5, 5.2f));
        return root;
    }

    private static Node BuildCharSelect()
    {
        var root = new Control { Name = "MumyeongCharSelectBg", MouseFilter = Control.MouseFilterEnum.Ignore, ClipContents = true };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var bg = Add(root, new TextureRect
        {
            Texture = Tex("char_select_bg.png"), MouseFilter = Control.MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
        }, "Bg");
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var stage = Add(root, new Node2D(), "Stage");
        var clip = Clip("idle", 24f, true);
        for (var i = 0; i < Figures.Length; i++)
        {
            var name = $"Spirit_{Figures[i]}";
            Add(stage, new Sprite2D
            {
                Texture = Tex($"spirit_{Figures[i]}.png"), Position = new Vector2(1450, 430), Modulate = new Color(1, 1, 1, 0),
            }, name);
            // each spirit rises behind him in turn: 4 s each
            var t0 = i * 4f;
            var keys = new List<(float, Variant)>();
            if (t0 > 0) keys.Add((0, new Color(1, 1, 1, 0)));
            keys.Add((t0, new Color(1, 1, 1, 0)));
            keys.Add((t0 + 1.4f, new Color(1, 1, 1, 0.7f)));
            keys.Add((t0 + 2.8f, new Color(1, 1, 1, 0.7f)));
            keys.Add((t0 + 4f, new Color(1, 1, 1, 0)));
            if (t0 + 4f < 24f) keys.Add((24f, new Color(1, 1, 1, 0)));
            clip.Key($"Stage/{name}:modulate", keys.ToArray());
            var moves = new List<(float, Variant)> { (0, new Vector2(1450, 430)) };
            if (t0 > 0) moves.Add((t0, new Vector2(1450, 430)));
            moves.Add((t0 + 4f, new Vector2(1450, 390)));
            if (t0 + 4f < 24f) moves.Add((24f, new Vector2(1450, 390)));
            clip.Key($"Stage/{name}:position", moves.ToArray());
        }

        Add(stage, new Sprite2D { Texture = Tex("reaper.png"), Position = new Vector2(1622, 605), Scale = Vector2.One * 1.15f },
            "Reaper");
        clip.Key("Stage/Reaper:position", (0, new Vector2(1622, 605)), (12f, new Vector2(1622, 598)), (24f, new Vector2(1622, 605)));
        AddPlayer(root, "AnimationPlayer", "idle", clip);
        return root;
    }

    // ------------------------------------------------------------------ helpers

    private static Sprite2D HiddenSpirit(Vector2 pos, float scale) =>
        new() { Visible = false, Position = pos, Scale = Vector2.One * scale, Modulate = new Color(1, 1, 1, 0) };

    private sealed class ClipBuilder(Animation anim)
    {
        public readonly Animation Anim = anim;

        public ClipBuilder Key(string path, params (float Time, Variant Value)[] keys)
        {
            var t = Anim.AddTrack(Animation.TrackType.Value);
            Anim.TrackSetPath(t, path);
            Anim.TrackSetInterpolationType(t, Animation.InterpolationType.Cubic);
            foreach (var (time, value) in keys) Anim.TrackInsertKey(t, time, value);
            return this;
        }

        public ClipBuilder Key(string path, params (float Time, Vector2 Value)[] keys) =>
            Key(path, keys.Select(k => (k.Time, (Variant)k.Value)).ToArray());

        public ClipBuilder Key(string path, params (float Time, Color Value)[] keys) =>
            Key(path, keys.Select(k => (k.Time, (Variant)k.Value)).ToArray());
    }

    private static ClipBuilder Clip(string name, float length, bool loop = false)
    {
        var anim = new Animation { ResourceName = name, Length = length };
        if (loop) anim.LoopMode = Animation.LoopModeEnum.Linear;
        return new ClipBuilder(anim);
    }

    private static ClipBuilder Float(string path, Vector2 pos, float amp, float length) =>
        Clip("float", length, true).Key(path, (0, pos), (length / 2, pos + new Vector2(0, -amp)), (length, pos));

    private static void AddPlayer(Node root, string name, string autoplay, params ClipBuilder[] clips)
    {
        var lib = new AnimationLibrary();
        foreach (var c in clips) lib.AddAnimation(c.Anim.ResourceName, c.Anim);
        var player = Add(root, new AnimationPlayer(), name);
        player.AddAnimationLibrary("", lib);
        player.Autoplay = autoplay;
    }
}
