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
        var covered = new bool[_world.Width, _world.Height];
        for (int y = 0; y < _world.Height; y++)
        for (int x = 0; x < _world.Width; x++)
        {
            if (!_world.Blocked[x, y] || covered[x, y]) continue;
            int width = 1;
            while (x + width < _world.Width && _world.Blocked[x + width, y] && !covered[x + width, y]) width++;
            int height = 1;
            while (y + height < _world.Height)
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
        int maxX = Math.Min(_world.Width - 1, (int)(_view.End.X / 32));
        int minY = Math.Max(0, (int)(_view.Position.Y / 32));
        int maxY = Math.Min(_world.Height - 1, (int)(_view.End.Y / 32));
        DrawRect(_view.Grow(40), new Color("#1e343a"));
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++) DrawTile(x, y);
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++) DrawDecoration(x, y);

        if (_world.Area != "world") return;

        if (IsInView(new(60, 59), 330)) DrawCentralRuins();
        if (IsInView(new(27, 32), 240)) DrawForestRunes();
        if (IsInView(new(94, 43), 240)) DrawDunes();
        if (IsInView(new(27, 82), 280)) DrawLake();
        if (IsInView(new(56, 85), 130)) DrawCamp();
        if (IsInView(new(72, 24), 120)) DrawOverlook();
        if (IsInView(new(89, 91), 130)) DrawArchive();
        if (IsInView(new(12, 54), 130)) DrawHollow();
        if (IsInView(new(48, 108), 130)) DrawBell();
        DrawExtendedLandmarks();
        foreach (WorldObject obj in _world.Objects)
            if (IsInView(obj.Tile, 120)) DrawWorldObject(obj);
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
            Terrain.Ruins => _world.Area == "forge" ? new Color("#5e6668") : Stone,
            Terrain.Wetland => new Color("#526f69"),
            Terrain.Snow => new Color("#a4b9bf"),
            _ => new Color("#8b8563")
        };
        if (_world.Area != "world")
        {
            basis = _world.Blocked[x, y]
                ? new Color(_world.Area == "forge" ? "#303d44" : "#293e3d")
                : terrain == Terrain.Path
                    ? new Color(_world.Area == "forge" ? "#566069" : "#607267") : basis;
        }
        else if (terrain == Terrain.Path)
        {
            Terrain nearby = _world.TileAt(x - 2, y);
            if (nearby == Terrain.Snow || _world.TileAt(x + 2, y) == Terrain.Snow) basis = new Color("#7f979e");
            else if (nearby == Terrain.Wetland) basis = new Color("#84907a");
        }
        DrawRect(new Rect2(p, new(32, 32)), basis.Lightened(shade));
        if (_world.Area != "world" && _world.Blocked[x, y])
        {
            Rect(p, 1, 1, 30, 2, new Color("#596c68"));
            Rect(p, 1, 29, 30, 3, new Color("#1b3032"));
            Rect(p, 30, 1, 2, 28, new Color("#1b3032"));
            if (h % 4 == 0) Rect(p, 3, 8, 9, 3, Moss);
            return;
        }
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
        if (terrain == Terrain.Snow)
        {
            Rect(p, 0, 27, 32, 3, new Color("#859da6", 0.25f));
            Rect(p, h % 23 + 2, (h >> 7) % 19 + 4, 8, 2, new Color("#d9e4df", 0.56f));
            if (h % 6 == 0) Rect(p, (h >> 9) % 23 + 2, 22, 3, 1, new Color("#79999d"));
            return;
        }
        if (terrain == Terrain.Wetland)
        {
            Rect(p, h % 19 + 3, (h >> 7) % 20 + 5, 11, 3, new Color("#6c8a78", 0.45f));
            Rect(p, (h >> 9) % 21 + 3, (h >> 14) % 25 + 1, 7, 1, new Color("#a7b898", 0.4f));
            if (h % 7 == 0) Rect(p, 10, 15, 4, 4, new Color("#918cab"));
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
        if (_world.Area != "world") return;
        if (terrain == Terrain.Path)
        {
            bool northSouth = _world.TileAt(x - 2, y) == Terrain.Water && _world.TileAt(x + 2, y) == Terrain.Water;
            bool eastWest = _world.TileAt(x, y - 2) == Terrain.Water && _world.TileAt(x, y + 2) == Terrain.Water;
            if (northSouth || eastWest)
            {
                Rect(p, -16, -16, 32, 32, new Color("#7e8066"));
                for (int plank = -12; plank <= 12; plank += 8)
                    if (northSouth) Rect(p, -15, plank, 30, 2, new Color("#b0aa83"));
                    else Rect(p, plank, -15, 2, 30, new Color("#b0aa83"));
                if (northSouth)
                {
                    Rect(p, -16, -16, 3, 32, new Color("#536b60"));
                    Rect(p, 13, -16, 3, 32, new Color("#536b60"));
                }
                else
                {
                    Rect(p, -16, -16, 32, 3, new Color("#536b60"));
                    Rect(p, -16, 13, 32, 3, new Color("#536b60"));
                }
            }
        }
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
        if (terrain == Terrain.Snow && _world.Blocked[x, y])
        {
            DrawRock(p);
            Polygon(p, new Color("#d1ded8"), new(-11, -9), new(1, -14), new(8, -7), new(-1, -3), new(-13, 2));
            return;
        }
        if (terrain == Terrain.Wetland)
        {
            if (_world.Blocked[x, y])
            {
                Rect(p, -11, 5, 23, 8, new Color("#354e49"));
                Polygon(p, new Color("#6c7660"), new(-12, 7), new(-7, -12), new(-4, -37), new(1, -32), new(4, -10), new(13, 8));
                Rect(p, -11, -23, 8, 3, new Color("#6c7660"));
                Rect(p, 2, -19, 12, 3, new Color("#6c7660"));
            }
            else if (h % 9 == 0)
                for (int i = -8; i <= 8; i += 4)
                {
                    Rect(p, i, -14 - (i & 3), 1, 29, new Color("#92a278"));
                    Rect(p, i - 1, -18 - (i & 3), 3, 7, new Color("#b6ac83"));
                }
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

    private void DrawExtendedLandmarks()
    {
        if (IsInView(new(154, 33), 200))
        {
            Vector2 p = WorldData.Center(new(154, 33));
            Rect(p, -92, -75, 184, 18, new Color("#847754"));
            Rect(p, -94, -80, 188, 7, new Color("#bfb18a"));
            for (int i = -1; i <= 1; i++) DrawPillar(p + new Vector2(i * 72, -8), 46, false);
            Rect(p, -43, 43, 37, 23, new Color("#8c6e4b"));
            Rect(p, 20, 45, 43, 19, new Color("#9e865c"));
            Rect(p, -82, 20, 16, 40, new Color("#c8b187"));
        }
        if (IsInView(new(177, 95), 230))
        {
            Vector2 p = WorldData.Center(new(177, 95));
            for (int i = -1; i <= 1; i++)
            {
                Vector2 hut = p + new Vector2(i * 96, -50 - Math.Abs(i) * 32);
                Rect(hut, -29, -4, 58, 36, new Color("#4c6259"));
                Polygon(hut, new Color("#778571"), new(-38, -1), new(0, -33), new(38, -1));
                Rect(hut, -8, 8, 16, 24, new Color("#2b4947"));
                Rect(hut, -25, 30, 6, 23, new Color("#748270"));
                Rect(hut, 19, 30, 6, 23, new Color("#748270"));
            }
            DrawLine(p + new Vector2(-132, 59), p + new Vector2(132, 59), new Color("#8c9780"), 8);
            for (int i = -4; i <= 4; i++) Rect(p, i * 30, 51, 3, 17, new Color("#bac1a0"));
        }
        if (IsInView(new(202, 132), 230))
        {
            Vector2 p = WorldData.Center(new(202, 132));
            for (int i = -2; i <= 2; i++)
            {
                if (i == 0) continue;
                DrawPillar(p + new Vector2(i * 58, -12), 64, false);
                Rect(p, i * 58 - 32, -82, 64, 12, new Color("#acb6a0"));
                Rect(p, i * 58 - 30, -70, 60, 8, new Color("#657e70"));
            }
            DrawRock(p + new Vector2(-10, 50));
            DrawRock(p + new Vector2(15, 62));
        }
        if (IsInView(new(97, 170), 190))
        {
            Vector2 p = WorldData.Center(new(97, 170));
            DrawPillar(p + new Vector2(-38, -25), 85, false);
            DrawPillar(p + new Vector2(38, -25), 85, false);
            Rect(p, -62, -117, 124, 13, new Color("#a8ac8b"));
            Rect(p, -45, -87, 90, 28, new Color("#465d50"));
            Rect(p, -47, -92, 94, 7, new Color("#839178"));
            Rect(p, -3, -153, 6, 46, new Color("#82795c"));
            Polygon(p, new Color("#bba879"), new(3, -150), new(38, -145), new(32, -126), new(3, -130));
        }
        if (IsInView(new(203, 199), 230))
        {
            Vector2 p = WorldData.Center(new(203, 199));
            Ring(p, 130, new Color("#d5e0d9", 0.5f), 4);
            Ring(p, 104, new Color("#607f88", 0.7f), 3);
            for (int i = 0; i < 8; i++)
            {
                Vector2 v = Vector2.FromAngle(i * Mathf.Tau / 8);
                DrawLine(p + v * 90, p + v * 125, new Color("#cbd5c9"), 3);
            }
            DrawPillar(p + new Vector2(-68, -22), 62, false);
            DrawPillar(p + new Vector2(68, -22), 62, false);
            Rect(p, -31, -4, 62, 27, new Color("#62808a"));
            Rect(p, -24, -26, 48, 28, new Color("#b7c6bd"));
            DrawCircle(p + new Vector2(0, -11), 17, new Color("#506e7b"));
            DrawLine(p + new Vector2(-13, -1), p + new Vector2(13, -20), new Color("#b9dcdf"), 4);
        }
        if (IsInView(new(44, 165), 180))
        {
            Vector2 p = WorldData.Center(new(44, 165));
            for (int i = 0; i < 6; i++) DrawTree(p + Vector2.FromAngle(i * Mathf.Tau / 6) * 110, 1);
            DrawPillar(p, 32, false);
            Ring(p, 49, new Color("#99b18e", 0.45f), 2);
        }
    }

    private void DrawWorldObject(WorldObject obj)
    {
        Vector2 p = WorldData.Center(obj.Tile);
        switch (obj.Kind)
        {
            case "checkpoint":
                if (obj.Id == "camp") return;
                bool visited = _state.Checkpoints.Contains(obj.Id);
                Rect(p, -54, 13, 108, 12, new Color("#253f3b", 0.4f));
                Color canvas = obj.Id == "snow_camp" ? new Color("#b5c3bf") : new Color("#aa9f78");
                Polygon(p + new Vector2(-48, -8), canvas, new(-31, 19), new(0, -29), new(32, 19));
                Polygon(p + new Vector2(-48, -8), new Color("#3a5048"), new(-14, 18), new(0, -16), new(12, 18));
                Ring(p + new Vector2(23, 10), 17, new Color("#9da78b"), 4);
                Rect(p, 9, 7, 27, 6, new Color("#655644"));
                Polygon(p + new Vector2(23, 10), visited ? Gold : new Color("#849477"), new(-8, 0), new(-4, -11), new(1, -8), new(4, -21), new(10, 0));
                if (visited) DrawLight(p + new Vector2(23, -1), Gold, 36);
                Rect(p, 50, -25, 3, 51, new Color("#8d8864"));
                Polygon(p + new Vector2(53, -24), visited ? Cyan : new Color("#8b9c82"), new(0, 0), new(26, 5), new(21, 19), new(0, 15));
                break;
            case "chest":
                bool opened = _state.OpenedChests.Contains(obj.Id);
                Rect(p, -18, 7, 38, 9, new Color("#223b35", 0.45f));
                Rect(p, -15, -5, 30, 21, new Color("#795e43"));
                Rect(p, -11, -3, 22, 13, new Color("#a08759"));
                Rect(p, -16, opened ? -18 : -9, 32, 9, new Color("#bcaa74"));
                Rect(p, -11, opened ? -15 : -7, 22, 5, new Color("#76664a"));
                Rect(p, -11, -3, 3, 18, new Color("#d3bd7d"));
                Rect(p, 8, -3, 3, 18, new Color("#d3bd7d"));
                Rect(p, -2, -2, 5, 7, opened ? new Color("#403f31") : Gold);
                if (!opened) Rect(p, 13, -16 + MathF.Sin(Clock * 2) * 2, 3, 3, Gold);
                break;
            case "portal":
                bool forge = obj.Value == "forge";
                Color glow = forge ? new Color("#efb574") : Cyan;
                DrawPillar(p + new Vector2(-45, 8), 68, true);
                DrawPillar(p + new Vector2(45, 8), 68, true);
                Rect(p, -63, -68, 126, 15, forge ? new Color("#847d68") : new Color("#8ca38a"));
                Rect(p, -29, -43, 58, 60, new Color("#182e33"));
                Rect(p, -27, 7, 54, 10, new Color("#708374"));
                Rect(p, -37, 18, 74, 8, new Color("#92a48d"));
                DrawGlyph(p + new Vector2(0, -60), forge ? 1 : 0, glow, 9);
                for (int i = 0; i < 4; i++)
                    Rect(p, -23 + i * 15, -38 + MathF.Sin(Clock + i) * 5, 2, 35, new Color(glow, 0.16f));
                DrawLight(p + new Vector2(0, -17), glow, 56);
                if (!forge)
                {
                    DrawLine(p + new Vector2(-70, 32), p + new Vector2(-36, -57), new Color("#456947"), 6);
                    DrawLine(p + new Vector2(66, 24), p + new Vector2(45, -61), new Color("#456947"), 5);
                }
                else
                {
                    Rect(p, -83, -37, 15, 62, new Color("#5b6363"));
                    Rect(p, 65, -41, 18, 68, new Color("#5b6363"));
                    Rect(p, -81, -33, 11, 8, new Color("#efaf74"));
                    Rect(p, 68, -37, 12, 8, new Color("#efaf74"));
                }
                break;
        }
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
            Terrain terrain = _world.TileAt(tx, ty);
            if (terrain == Terrain.Snow)
            {
                p.Y += (Clock * 14 + h % 60) % 80;
                p.X += MathF.Sin(Clock * 0.6f + h % 37) * 11;
                Rect(p, 0, 0, 2, 2, new Color("#edf1e6", 0.65f));
                continue;
            }
            if (terrain is not (Terrain.Forest or Terrain.Meadow or Terrain.Wetland)) continue;
            p += new Vector2(MathF.Sin(Clock * 0.22f + h % 60) * 15, MathF.Cos(Clock * 0.18f + h % 31) * 7);
            float glow = (MathF.Sin(Clock * 0.6f + h % 17) + 1) * 0.3f;
            Rect(p, 0, 0, 2, 2, new Color(Gold, glow));
        }
    }
}
