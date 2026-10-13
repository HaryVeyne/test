using Godot;
using System;

namespace ProjectUnknown;

public partial class WorldRenderer : Node2D
{
    private WorldData _world = null!;
    private GameState _state = null!;
    private StaticBody2D? _collisions;
    private Rect2 _view;
    public Vector2 Focus { get; set; }
    public float PlateCharge { get; set; }
    public float Clock { get; set; }

    private static readonly Color Grass = new("#52684b");
    private static readonly Color Forest = new("#354e43");
    private static readonly Color Sand = new("#b49b67");
    private static readonly Color Water = new("#244d59");
    private static readonly Color Stone = new("#71796f");
    private static readonly Color Shadow = new("#243a37");
    private static readonly Color Cyan = new("#9cdac5");
    private static readonly Color Gold = new("#f0d490");
    private static readonly Color Ink = new("#253536");
    private static readonly Color Moss = new("#5a7960");

    public void Configure(WorldData world, GameState state)
    {
        _world = world;
        _state = state;
        Focus = world.Spawn;
        RefreshCollisions();
        QueueRedraw();
    }

    public void RefreshCollisions()
    {
        _collisions?.QueueFree();
        _collisions = new StaticBody2D { Name = "TerrainCollision", CollisionLayer = 1, CollisionMask = 0 };
        AddChild(_collisions);
        var covered = new bool[WorldData.Size, WorldData.Size];
        for (int y = 0; y < WorldData.Size; y++)
        for (int x = 0; x < WorldData.Size; x++)
        {
            if (!_world.Blocked[x, y] || covered[x, y]) continue;
            int width = 1;
            while (x + width < WorldData.Size && _world.Blocked[x + width, y] && !covered[x + width, y]) width++;
            int height = 1;
            while (y + height < WorldData.Size)
            {
                bool complete = true;
                for (int i = 0; i < width; i++)
                    if (!_world.Blocked[x + i, y + height] || covered[x + i, y + height]) { complete = false; break; }
                if (!complete) break;
                height++;
            }
            for (int xx = x; xx < x + width; xx++)
            for (int yy = y; yy < y + height; yy++) covered[xx, yy] = true;
            _collisions.AddChild(new CollisionShape2D
            {
                Position = new Vector2((x + width * 0.5f) * 32, (y + height * 0.5f) * 32),
                Shape = new RectangleShape2D { Size = new Vector2(width * 32, height * 32) }
            });
        }
    }

    public override void _Draw()
    {
        if (_world is null) return;
        Vector2 viewport = GetViewportRect().Size;
        Vector2 zoom = GetViewport().GetCamera2D()?.Zoom ?? Vector2.One;
        Vector2 half = viewport / zoom * 0.5f + new Vector2(100, 100);
        _view = new Rect2(Focus - half, half * 2);
        int minX = Math.Max(0, (int)(_view.Position.X / 32));
        int maxX = Math.Min(119, (int)(_view.End.X / 32));
        int minY = Math.Max(0, (int)(_view.Position.Y / 32));
        int maxY = Math.Min(119, (int)(_view.End.Y / 32));
        DrawRect(_view.Grow(40), new Color("#1e343a"));
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++) DrawTile(x, y);
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++) DrawDecoration(x, y);

        if (IsInView(new(60, 59), 330)) DrawCentralRuins();
        if (IsInView(new(27, 32), 240)) DrawForestRunes();
        if (IsInView(new(94, 43), 240)) DrawDunes();
        if (IsInView(new(27, 82), 280)) DrawLake();
        if (IsInView(new(56, 85), 130)) DrawCamp();
        if (IsInView(new(72, 24), 120)) DrawOverlook();
        if (IsInView(new(89, 91), 130)) DrawArchive();
        if (IsInView(new(12, 54), 130)) DrawHollow();
        if (IsInView(new(48, 108), 130)) DrawBell();
        DrawFloatingMotes();
    }

    private bool IsInView(Vector2I tile, float radius) => _view.Grow(radius).HasPoint(WorldData.Center(tile));
    private bool Active(string id) => _state.Activated.Contains(id);
    private uint Variation(int x, int y) => WorldData.Hash(x, y, _world.Seed);
    private void Rect(Vector2 at, float x, float y, float w, float h, Color color) => DrawRect(new Rect2(at + new Vector2(x, y), new Vector2(w, h)), color);
    private void Polygon(Vector2 at, Color color, params Vector2[] points)
    {
        for (int i = 0; i < points.Length; i++) points[i] += at;
        DrawColoredPolygon(points, color);
    }
    private void Ring(Vector2 center, float radius, Color color, float width = 1) => DrawArc(center, radius, 0, Mathf.Tau, 64, color, width);

    private void DrawTile(int x, int y)
    {
        Vector2 p = new(x * 32, y * 32);
        uint h = Variation(x, y);
        Terrain terrain = _world.Tiles[x, y];
        float shade = ((h % 9) - 4f) * 0.012f;
        Color basis = terrain switch
        {
            Terrain.Meadow => Grass,
            Terrain.Forest => Forest,
            Terrain.Dunes => Sand,
            Terrain.Water => Water,
            Terrain.Ruins => Stone,
            _ => new Color("#8b8563")
        };
        DrawRect(new Rect2(p, new(32, 32)), basis.Lightened(shade));
        if (terrain == Terrain.Water)
        {
            Color ripple = new Color("#588687", 0.30f);
            float offset = (MathF.Sin(Clock * 0.65f + h % 25) * 3);
            Rect(p, (h % 17) + offset, (h >> 5) % 23 + 4, 11, 1, ripple);
            Rect(p, ((h >> 11) % 21) - offset, (h >> 14) % 19 + 7, 5, 1, ripple);
            if (_world.TileAt(x, y - 1) != Terrain.Water) Rect(p, 0, 0, 32, 3, new Color("#1c3940"));
            if (_world.TileAt(x - 1, y) != Terrain.Water) Rect(p, 0, 0, 2, 32, new Color("#315d5a"));
            if (_world.TileAt(x, y + 1) != Terrain.Water) Rect(p, 0, 29, 32, 3, new Color("#83a494", 0.45f));
            return;
        }
        if (terrain == Terrain.Ruins)
        {
            Rect(p, 0, 29, 32, 3, new Color("#525f59"));
            Rect(p, 30, 0, 2, 29, new Color("#525f59"));
            Rect(p, 2, 2, 26, 1, new Color("#939a86", 0.5f));
            if (h % 5 == 0)
            {
                DrawLine(p + new Vector2(5, 13), p + new Vector2(13, 18), new Color("#525f59"));
                DrawLine(p + new Vector2(13, 18), p + new Vector2(11, 27), new Color("#525f59"));
            }
            if (h % 3 == 0) Rect(p, 2, 26, 9, 3, Moss);
            return;
        }
        if (terrain == Terrain.Dunes)
        {
            Color line = new("#cbb584", 0.55f);
            int offset = (int)(h % 15);
            DrawLine(p + new Vector2(0, offset + 7), p + new Vector2(14, offset + 3), line);
            DrawLine(p + new Vector2(14, offset + 3), p + new Vector2(31, offset + 4), line);
            Rect(p, h % 27, (h >> 8) % 27, 2, 1, new Color("#897f5e"));
            return;
        }
        if (terrain == Terrain.Path)
        {
            Rect(p, h % 26 + 2, (h >> 7) % 23 + 3, 4, 2, new Color("#aaa27a", 0.45f));
            Rect(p, (h >> 11) % 24 + 3, (h >> 16) % 25, 2, 1, new Color("#666b50"));
            if (h % 5 == 0) DrawLine(p + new Vector2(9, 17), p + new Vector2(21, 17), new Color("#716e51", 0.32f));
            return;
        }
        Color blade = terrain == Terrain.Forest ? new Color("#537259", 0.64f) : new Color("#829070", 0.64f);
        int gx = (int)(h % 23) + 4, gy = (int)((h >> 7) % 20) + 6;
        Rect(p, gx, gy, 1, 5, blade);
        Rect(p, gx - 2, gy + 1, 1, 3, blade);
        Rect(p, gx + 2, gy - 1, 1, 4, blade);
        if (h % 11 == 0)
        {
            Color flower = h % 22 == 0 ? new("#c7b991") : new("#96b9a3");
            Rect(p, (h >> 11) % 24 + 3, (h >> 18) % 23 + 4, 2, 2, flower);
        }
    }

    private void DrawDecoration(int x, int y)
    {
        Terrain terrain = _world.Tiles[x, y];
        Vector2 p = WorldData.Center(new(x, y));
        uint h = Variation(x, y);
        if (terrain == Terrain.Forest && _world.Blocked[x, y])
        {
            DrawTree(p, (int)(h % 3));
            return;
        }
        if (terrain == Terrain.Ruins && _world.Blocked[x, y])
        {
            DrawPillar(p, 45, false);
            return;
        }
        if (terrain == Terrain.Dunes && _world.Blocked[x, y])
        {
            DrawRock(p, true);
            return;
        }
        if (terrain is Terrain.Meadow or Terrain.Forest && h % 27 == 0)
        {
            Rect(p, -8, 5, 17, 4, new Color("#2a443b", 0.45f));
            Rect(p, -7, 0, 13, 7, new Color("#425e43"));
            Rect(p, -4, -3, 9, 6, new Color("#6a7f53"));
            if (h % 81 == 0) Rect(p, -2, -1, 2, 2, new Color("#d2af82"));
        }
        if (terrain == Terrain.Dunes && h % 39 == 0)
        {
            Rect(p, -2, -10, 5, 20, new Color("#60745a"));
            Rect(p, -7, -5, 5, 3, new Color("#60745a"));
            Rect(p, -8, -11, 3, 8, new Color("#60745a"));
            Rect(p, 2, 0, 5, 3, new Color("#60745a"));
            Rect(p, 5, -6, 3, 9, new Color("#60745a"));
            Rect(p, -1, -9, 1, 16, new Color("#91a07b"));
        }
        if (terrain != Terrain.Water && (_world.TileAt(x, y + 1) == Terrain.Water || _world.TileAt(x + 1, y) == Terrain.Water) && h % 3 == 0)
        {
            for (int i = -5; i < 7; i += 4)
            {
                Rect(p, i, 8, 1, 9, new Color("#87916d"));
                Rect(p, i - 1, 6, 3, 5, new Color("#b0a270"));
            }
        }
    }

    private void DrawTree(Vector2 p, int variety)
    {
        Rect(p, -14, 8, 31, 9, new Color("#20382f", 0.65f));
        Rect(p, -4, -5, 8, 22, new Color("#5e5c42"));
        Rect(p, -1, 0, 2, 15, new Color("#968367"));
        Color dark = new("#253e35"), middle = new("#3b5b44"), light = new("#607950");
        if (variety == 0)
        {
            Polygon(p, dark, new(-20, 6), new(-14, -9), new(-20, -9), new(-10, -22), new(-14, -22), new(0, -41), new(14, -22), new(10, -22), new(20, -9), new(14, -9), new(20, 6));
            Polygon(p, middle, new(-16, 1), new(-10, -12), new(-14, -12), new(-7, -25), new(-9, -25), new(0, -37), new(6, -24), new(11, -13), new(7, -13), new(15, 1));
            Rect(p, -7, -12, 9, 2, light);
            Rect(p, -2, -26, 4, 2, light);
        }
        else
        {
            Polygon(p, dark, new(-19, -3), new(-23, -15), new(-16, -27), new(-7, -32), new(5, -34), new(19, -23), new(22, -10), new(15, 0), new(2, 3), new(-10, 1));
            Polygon(p, middle, new(-18, -14), new(-13, -26), new(-3, -30), new(9, -27), new(17, -19), new(13, -8), new(2, -5), new(-10, -6));
            Rect(p, -9, -27, 12, 3, light);
            Rect(p, -15, -20, 5, 3, light);
            Rect(p, 5, -17, 7, 2, new Color("#52734c"));
        }
    }

    private void DrawRock(Vector2 p, bool sand = false)
    {
        Rect(p, -15, 7, 30, 7, new Color("#374539", 0.4f));
        Polygon(p, sand ? new Color("#716e5d") : new Color("#657266"), new(-15, 5), new(-11, -11), new(2, -16), new(13, -8), new(15, 7), new(-5, 11));
        Polygon(p, sand ? new Color("#a09a79") : new Color("#899486"), new(-11, -9), new(1, -13), new(8, -7), new(-1, -3), new(-13, 2));
        DrawLine(p + new Vector2(2, -9), p + new Vector2(0, 5), new Color("#515e53"));
    }

    private void DrawPillar(Vector2 p, float height, bool lit)
    {
        Rect(p, -20, 9, 44, 12, new Color("#293d39", 0.5f));
        Rect(p, -17, 2, 34, 13, new Color("#535f57"));
        Rect(p, -14, -height, 28, height + 4, new Color("#6f7d70"));
        Rect(p, -10, -height + 3, 5, height - 1, new Color("#a2aa90"));
        Rect(p, 7, -height + 2, 6, height, new Color("#4c6257"));
        Rect(p, -19, -height - 5, 38, 8, new Color("#919b83"));
        Rect(p, -13, 0, 20, 3, Moss);
        Rect(p, -3, -height + 12, 6, 12, lit ? Cyan : new Color("#405b51"));
        if (lit) DrawCircle(p + new Vector2(0, -height + 18), 12, new Color(Cyan, 0.08f));
    }

    private void DrawForestRunes()
    {
        Vector2 altar = WorldData.Center(new(27, 32));
        bool done = Active("forest");
        Ring(altar, 115, new Color("#8ba27a", 0.24f));
        Ring(altar, 118, new Color("#233e33", 0.3f));
        for (int i = 0; i < 12; i++)
        {
            Vector2 leaf = altar + Vector2.FromAngle(i * Mathf.Tau / 12) * 111;
            Rect(leaf, -2, -2, 4, 4, new Color("#95a27c", 0.5f));
        }
        Vector2I[] tiles = { new(24, 32), new(27, 29), new(30, 32) };
        int[] marks = { 2, 1, 3 };
        for (int i = 0; i < 3; i++)
        {
            Vector2 p = WorldData.Center(tiles[i]);
            bool lit = done || (i == 1 && _state.RuneStep > 0) || (i == 0 && _state.RuneStep > 1);
            if (done) DrawLine(p, altar, new Color(Cyan, 0.4f), 2);
            DrawRock(p);
            Rect(p, -10, -24, 20, 27, new Color("#657c68"));
            Rect(p, -7, -24, 6, 25, new Color("#94a58b"));
            Color ink = lit ? Cyan : new Color("#d0c59b");
            DrawGlyph(p + new Vector2(0, -12), i, ink, 8);
            for (int m = 0; m < marks[i]; m++)
            {
                Rect(p, -((marks[i] - 1) * 4) + m * 8 - 2, -34, 4, 4, Gold);
                Rect(p, -((marks[i] - 1) * 4) + m * 8 - 2, -28, 4, 1, new Color("#638e70"));
            }
            if (lit) Ring(p + new Vector2(0, -10), 25 + MathF.Sin(Clock * 2 + i) * 2, new Color(Cyan, 0.24f));
        }
        Polygon(altar, new Color("#365447"), new(-22, 14), new(-24, -2), new(-11, -15), new(12, -15), new(24, -2), new(22, 14));
        DrawGlyph(altar + new Vector2(0, -2), 0, done ? Cyan : new Color("#85977b"), 13);
        if (done) DrawLight(altar + new Vector2(0, -18), Cyan, 34);
    }

    private void DrawGlyph(Vector2 p, int kind, Color color, float size)
    {
        switch (kind % 3)
        {
            case 0:
                DrawLine(p + new Vector2(-size, size * 0.6f), p + new Vector2(0, -size), color, 2);
                DrawLine(p + new Vector2(0, -size), p + new Vector2(size, size * 0.6f), color, 2);
                DrawLine(p + new Vector2(-size, size * 0.6f), p + new Vector2(size, size * 0.6f), color, 2);
                break;
            case 1:
                Ring(p, size * 0.85f, color, 2);
                DrawLine(p + new Vector2(0, -size - 3), p + new Vector2(0, size + 3), color, 2);
                break;
            case 2:
                DrawLine(p + new Vector2(-size, 0), p + new Vector2(0, -size), color, 2);
                DrawLine(p + new Vector2(0, -size), p + new Vector2(size, 0), color, 2);
                DrawLine(p + new Vector2(size, 0), p + new Vector2(0, size), color, 2);
                DrawLine(p + new Vector2(0, size), p + new Vector2(-size, 0), color, 2);
                Rect(p, -2, -2, 4, 4, color);
                break;
        }
    }

    private void DrawDunes()
    {
        Vector2 p = WorldData.Center(new(94, 43));
        bool done = Active("dunes");
        Ring(p, 126, new Color("#e5d5a5", 0.42f));
        Ring(p, 122, new Color("#8b825f", 0.5f));
        for (int i = 0; i < 16; i++)
        {
            Vector2 d = Vector2.FromAngle(i * Mathf.Tau / 16);
            DrawLine(p + d * 115, p + d * 126, new Color("#e1c990", 0.7f), 2);
        }
        Vector2I[] tiles = { new(91, 43), new(97, 43) };
        for (int i = 0; i < 2; i++)
        {
            Vector2 mirror = WorldData.Center(tiles[i]);
            Vector2 dir = Vector2.FromAngle(_state.Mirrors[i] * Mathf.Pi / 2);
            DrawLine(mirror, mirror + dir * 88, new Color(Gold, done ? 0.55f : 0.25f), 5);
            DrawLine(mirror, mirror + dir * 88, Gold, 1);
            Ring(mirror, 25, new Color("#706e57"), 2);
            Rect(mirror, -17, -7, 34, 18, new Color("#7d8067"));
            Rect(mirror, -14, -21, 28, 20, new Color("#455e5b"));
            Rect(mirror, -11, -18, 22, 12, new Color("#a0c1b1"));
            Rect(mirror, -8, -16, 12, 2, new Color("#e0e3c7"));
            Vector2 tip = mirror + dir * 29;
            DrawLine(mirror + new Vector2(0, -3), tip + new Vector2(0, -3), Gold, 3);
            Vector2 side = dir.Orthogonal() * 6;
            Polygon(tip + new Vector2(0, -3), Gold, dir * 5, -dir * 4 + side, -dir * 4 - side);
        }
        Rect(p, -25, 0, 50, 18, new Color("#777c67"));
        Rect(p, -19, -8, 38, 16, new Color("#a6a991"));
        DrawPillar(p, 38, done);
        Ring(p + new Vector2(0, -33), 15, done ? Gold : new Color("#c5b384"), 3);
        Rect(p, -26, -1, 6, 7, Gold);
        Rect(p, 20, -1, 6, 7, Gold);
        if (done) DrawLight(p + new Vector2(0, -38), Gold, 40);
    }

    private void DrawLake()
    {
        Vector2 plate = WorldData.Center(new(23, 82));
        bool bridge = Active("bridge"), done = Active("lake");
        Rect(plate, -21, -15, 42, 31, new Color("#354f48"));
        Rect(plate, -17, -11, 34, 24, new Color("#879985"));
        Rect(plate, -14, -8, 28, 17, new Color("#3e675f"));
        DrawGlyph(plate, 2, new Color("#bed0ad"), 7);
        if (PlateCharge > 0) DrawArc(plate, 23, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * PlateCharge, 40, Cyan, 3);
        for (int i = 0; i < 3; i++)
        {
            Vector2 at = WorldData.Center(new(26 + i, 82));
            if (bridge)
            {
                Rect(at, -16, -29, 32, 58, new Color("#7f9588"));
                Rect(at, -16, -27, 32, 2, new Color("#a1b49a"));
                Rect(at, -16, 25, 32, 4, new Color("#3e635c"));
                Rect(at, -15, -23, 1, 47, new Color("#546f66"));
                Rect(at, -2, -1, 4, 2, Cyan);
            }
            else
            {
                Rect(at, -12, -24, 24, 4, new Color("#4c7878", 0.7f));
                Rect(at, -12, 21, 24, 4, new Color("#4c7878", 0.7f));
                Rect(at, -2, -1, 4, 2, new Color("#75a6a1", 0.5f));
            }
        }
        Vector2 beacon = WorldData.Center(new(31, 82));
        Ring(beacon, 47, new Color("#bcc4a0", 0.45f));
        DrawPillar(beacon, 49, done);
        DrawGlyph(beacon + new Vector2(0, -32), 2, done ? Cyan : new Color("#b9c6a5"), 8);
        Polygon(beacon + new Vector2(0, -66), done ? Cyan : new Color("#76978b"), new(0, -10), new(8, 0), new(0, 11), new(-8, 0));
        if (done) DrawLight(beacon + new Vector2(0, -67), Cyan, 43);
    }

    private void DrawCentralRuins()
    {
        Vector2 p = WorldData.Center(new(60, 59));
        bool unlocked = Active("center"), ended = _state.Completed;
        Color line = new("#b0b299", 0.42f);
        Ring(p, 180, line, 2);
        Ring(p, 175, new Color("#354f48", 0.55f));
        Ring(p, 130, line);
        Ring(p, 105, new Color("#8f9d83", 0.42f), 2);
        for (int i = 0; i < 32; i++)
        {
            float angle = Mathf.Tau * i / 32;
            Vector2 direction = Vector2.FromAngle(angle);
            DrawLine(p + direction * 164, p + direction * 174, line, 2);
        }
        string[] keys = { "forest", "dunes", "lake" };
        for (int i = 0; i < 3; i++)
        {
            float angle = -Mathf.Pi / 2 + i * Mathf.Tau / 3;
            Vector2 at = p + Vector2.FromAngle(angle) * 101;
            bool lit = Active(keys[i]);
            DrawCircle(at, 23, new Color("#394f47"));
            Ring(at, 21, new Color("#a3b096"), 2);
            DrawGlyph(at, i, lit ? (i == 1 ? Gold : Cyan) : new Color("#697e6b"), 12);
            if (lit) DrawLine(at, p, new Color(i == 1 ? Gold : Cyan, 0.32f), 2);
        }
        foreach (Vector2I tile in new Vector2I[] { new(55, 54), new(65, 54), new(55, 64), new(65, 64) })
        {
            Vector2 at = WorldData.Center(tile);
            DrawPillar(at, 45, unlocked);
            Rect(at, -22, 17, 10, 5, new Color("#7b8a73"));
        }
        DrawArch(p + new Vector2(0, -165), true, unlocked);
        DrawArch(p + new Vector2(-172, 0), false, unlocked);
        DrawArch(p + new Vector2(172, 0), false, unlocked);
        Rect(p, -49, 25, 98, 14, new Color("#384f49"));
        Rect(p, -42, 11, 84, 21, new Color("#7e8d7b"));
        Rect(p, -34, 3, 68, 13, new Color("#a0a58b"));
        Rect(p, -23, -55, 46, 60, new Color("#324c4c"));
        Rect(p, -19, -53, 38, 56, new Color("#55716a"));
        Rect(p, -15, -50, 6, 48, new Color("#869e88"));
        Rect(p, -27, -61, 54, 9, new Color("#a8ae93"));
        DrawGlyph(p + new Vector2(0, -29), 2, unlocked ? Cyan : new Color("#8b9b82"), 14);
        if (unlocked)
        {
            float pulse = MathF.Sin(Clock * 1.5f) * 0.08f + 0.2f;
            DrawLight(p + new Vector2(0, -37), Cyan, ended ? 115 : 60);
            Ring(p, 73 + MathF.Sin(Clock) * 3, new Color(Cyan, pulse), 2);
            Polygon(p + new Vector2(0, -80), new Color(Cyan, 0.8f), new(0, -17), new(11, 0), new(0, 16), new(-11, 0));
        }
        else
            Polygon(p + new Vector2(0, -80), new Color("#647e72"), new(0, -17), new(11, 0), new(0, 16), new(-11, 0));
    }

    private void DrawArch(Vector2 p, bool horizontal, bool lit)
    {
        if (horizontal)
        {
            DrawPillar(p + new Vector2(-46, 0), 62, lit);
            DrawPillar(p + new Vector2(46, 0), 62, lit);
            Rect(p, -65, -66, 130, 16, new Color("#526e62"));
            Rect(p, -65, -66, 130, 5, new Color("#a5ad91"));
            Rect(p, -28, -61, 57, 7, new Color("#849782"));
            DrawGlyph(p + new Vector2(0, -58), 1, lit ? Cyan : new Color("#bec4a5"), 7);
        }
        else
        {
            DrawPillar(p + new Vector2(0, -43), 31, lit);
            DrawPillar(p + new Vector2(0, 43), 31, lit);
        }
    }

    private void DrawCamp()
    {
        Vector2 p = WorldData.Center(new(56, 85));
        Vector2 tent = p + new Vector2(-40, -11);
        Rect(tent, -37, 18, 78, 12, new Color("#283e35", 0.4f));
        Polygon(tent, new Color("#b5ad81"), new(-36, 20), new(-5, -27), new(18, -22), new(37, 20));
        Polygon(tent, new Color("#8c8c69"), new(-5, -27), new(18, -22), new(37, 20), new(5, 20));
        Polygon(tent, new Color("#354b41"), new(-24, 19), new(-5, -17), new(3, 19));
        DrawLine(tent + new Vector2(-5, -30), tent + new Vector2(-39, 22), new Color("#d4c89a"));
        Rect(p, 25, -8, 27, 13, new Color("#738b79"));
        Rect(p, 24, -9, 5, 15, new Color("#aac0a2"));
        Rect(p, 40, 12, 13, 16, new Color("#84735b"));
        Rect(p, 43, 9, 7, 3, new Color("#baa67b"));
        Vector2 fire = p + new Vector2(0, 31);
        Ring(fire, 16, new Color("#8d9478"), 4);
        DrawLight(fire + new Vector2(0, -8), Gold, 41);
        Rect(fire, -9, 0, 19, 5, new Color("#6f5540"));
        float flicker = MathF.Sin(Clock * 9) * 3;
        Polygon(fire, new Color("#d99d59"), new(-8, 2), new(-6, -10), new(-1, -5), new(3, -19 - flicker), new(8, -5), new(6, 2));
        Polygon(fire, Gold, new(-3, 1), new(-2, -8), new(2, -4), new(4, -13), new(5, 1));
    }

    private void DrawOverlook()
    {
        Vector2 p = WorldData.Center(new(72, 24));
        Ring(p, 68, new Color("#a9aa83", 0.4f), 2);
        for (int i = 0; i < 5; i++)
        {
            Vector2 at = p + new Vector2((i - 2) * 26, -36 + Math.Abs(i - 2) * 9);
            DrawRock(at);
        }
        Rect(p, -3, -67, 6, 70, new Color("#867b59"));
        float sway = MathF.Sin(Clock * 1.5f) * 4;
        Polygon(p, new Color("#bcb592"), new(3, -64), new(34, -59 + sway), new(29, -39 + sway), new(3, -43));
        Rect(p, -12, 4, 25, 4, new Color("#8d987c"));
        DrawGlyph(p + new Vector2(0, 22), 1, new Color("#b9bea0"), 8);
    }

    private void DrawArchive()
    {
        Vector2 p = WorldData.Center(new(89, 91));
        Rect(p, -65, -55, 130, 18, new Color("#536b61"));
        Rect(p, -65, -59, 130, 5, new Color("#9da78d"));
        Rect(p, -65, -55, 17, 79, new Color("#62786b"));
        Rect(p, 47, -55, 17, 79, new Color("#62786b"));
        Rect(p, -47, -36, 4, 54, Moss);
        DrawPillar(p + new Vector2(-46, 26), 22, false);
        DrawPillar(p + new Vector2(46, 26), 34, false);
        Rect(p, -23, -13, 47, 27, new Color("#344f47"));
        Rect(p, -20, -19, 41, 28, new Color("#8c9d84"));
        for (int i = 0; i < 3; i++) DrawGlyph(p + new Vector2((i - 1) * 13, -6), i, new Color("#d7d1a8"), 4);
        Rect(p, -12, 15, 24, 3, new Color("#c7c3a2"));
    }

    private void DrawHollow()
    {
        Vector2 p = WorldData.Center(new(12, 54));
        Rect(p, -41, 19, 84, 13, new Color("#2a4238", 0.5f));
        Polygon(p, new Color("#575d43"), new(-32, 23), new(-25, -28), new(-15, -66), new(4, -73), new(29, -31), new(37, 22));
        Polygon(p, new Color("#7b7955"), new(-25, 14), new(-18, -34), new(-9, -64), new(0, -64), new(-5, -8), new(-13, 19));
        Rect(p, 20, -39, 11, 10, new Color("#575d43"));
        Rect(p, 27, -46, 18, 8, new Color("#575d43"));
        Rect(p, -36, -45, 15, 11, new Color("#575d43"));
        Polygon(p, new Color("#223c34"), new(-9, 17), new(-11, -7), new(-4, -22), new(7, -23), new(17, -4), new(16, 18));
        Rect(p, -2, 2, 11, 3, new Color("#82a786"));
        Rect(p, -26, 16, 18, 5, Moss);
        Rect(p, 21, 18, 23, 5, Moss);
        DrawLight(p + new Vector2(4, 4), Cyan, 22);
    }

    private void DrawBell()
    {
        Vector2 p = WorldData.Center(new(48, 108));
        DrawPillar(p + new Vector2(-34, 0), 66, false);
        DrawPillar(p + new Vector2(34, 0), 66, false);
        Rect(p, -50, -71, 100, 14, new Color("#a2aa8d"));
        Rect(p, -2, -57, 4, 19, new Color("#5a7462"));
        Polygon(p, new Color("#a19b70"), new(-13, -23), new(-10, -38), new(0, -44), new(10, -38), new(13, -23), new(19, -17), new(-19, -17));
        Rect(p, -15, -21, 29, 3, new Color("#d1c692"));
        Rect(p, -2, -15, 4, 5, new Color("#d1c692"));
        DrawGlyph(p + new Vector2(0, 20), 1, new Color("#bac1a3"), 11);
    }

    private void DrawLight(Vector2 p, Color color, float radius)
    {
        DrawCircle(p, radius, new Color(color, 0.035f));
        DrawCircle(p, radius * 0.67f, new Color(color, 0.05f));
        DrawCircle(p, radius * 0.35f, new Color(color, 0.08f));
    }

    private void DrawFloatingMotes()
    {
        int x0 = (int)(Focus.X / 160), y0 = (int)(Focus.Y / 160);
        for (int x = x0 - 4; x <= x0 + 4; x++)
        for (int y = y0 - 3; y <= y0 + 3; y++)
        {
            uint h = Variation(x * 5, y * 5);
            Vector2 p = new(x * 160 + h % 140, y * 160 + (h >> 8) % 140);
            int tx = (int)(p.X / 32), ty = (int)(p.Y / 32);
            if (_world.TileAt(tx, ty) is not (Terrain.Forest or Terrain.Meadow)) continue;
            p += new Vector2(MathF.Sin(Clock * 0.22f + h % 60) * 15, MathF.Cos(Clock * 0.18f + h % 31) * 7);
            float glow = (MathF.Sin(Clock * 0.6f + h % 17) + 1) * 0.3f;
            Rect(p, 0, 0, 2, 2, new Color(Gold, glow));
        }
    }
}
