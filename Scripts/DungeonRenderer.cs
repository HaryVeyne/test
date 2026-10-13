using Godot;
using System;

namespace ProjectUnknown;

public partial class DungeonRenderer : Node2D
{
    private WorldData _world = null!;
    private GameState _state = null!;
    private Rect2 _view;
    public float Clock { get; set; }
    public Vector2 Focus { get; set; }
    private bool Crypt => _world.Area == "crypt";
    private static readonly Color Ink = new("#172827");
    private static readonly Color Stone = new("#536962");
    private static readonly Color Light = new("#a4d5ba");
    private static readonly Color Brass = new("#b89960");
    private static readonly Color Metal = new("#424b52");
    private static readonly Color Fire = new("#ef9860");

    public void Configure(WorldData world, GameState state)
    {
        _world = world;
        _state = state;
        Focus = world.Spawn;
    }

    public override void _Draw()
    {
        Vector2 zoom = GetViewport().GetCamera2D()?.Zoom ?? Vector2.One;
        Vector2 half = GetViewportRect().Size / zoom * 0.5f + new Vector2(64, 64);
        _view = new(Focus - half, half * 2);
        int minX = Math.Max(0, (int)_view.Position.X / 32);
        int minY = Math.Max(0, (int)_view.Position.Y / 32);
        int maxX = Math.Min(_world.Width - 1, (int)_view.End.X / 32);
        int maxY = Math.Min(_world.Height - 1, (int)_view.End.Y / 32);
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            if (_world.Blocked[x, y] && !(x == 38 && y >= 20 && y <= 24)) DrawWall(x, y);
            else if (!_world.Blocked[x, y]) DrawFloorDetail(x, y);
        }
        if (Crypt) DrawCryptProps();
        else DrawForgeProps();
        DrawGate();
        DrawArena();
        foreach (WorldObject obj in _world.Objects)
        {
            Vector2 at = WorldData.Center(obj.Tile);
            if (!_view.Grow(60).HasPoint(at)) continue;
            switch (obj.Kind)
            {
                case "switch": DrawSwitch(at, _state.Activated.Contains(obj.Id)); break;
                case "chest": DrawChest(at, _state.OpenedChests.Contains(obj.Id)); break;
                case "exit": DrawExit(at); break;
                case "shrine": DrawPlaque(at); break;
            }
        }
    }

    private void Rect(Vector2 at, float x, float y, float width, float height, Color color) =>
        DrawRect(new Rect2(at + new Vector2(x, y), new Vector2(width, height)), color);
    private void Ring(Vector2 at, float radius, Color color, float width = 1) =>
        DrawArc(at, radius, 0, Mathf.Tau, 48, color, width);
    private bool Near(Vector2I tile, float margin = 120) => _view.Grow(margin).HasPoint(WorldData.Center(tile));

    private void DrawWall(int x, int y)
    {
        Vector2 p = new(x * 32, y * 32);
        uint hash = WorldData.Hash(x, y, _world.Seed);
        bool face = !_world.IsBlocked(x, y + 1);
        Rect(p, 0, 0, 32, 32, Crypt ? new("#263b36") : new("#232a2f"));
        if (Crypt)
        {
            Rect(p, 1, 1, 30, face ? 16 : 29, Stone.Darkened((hash % 5) * 0.05f));
            Rect(p, 2, 2, 28, 2, new("#6d7f71"));
            Rect(p, hash % 12 + 6, 5, 1, 8, new("#344d43"));
            if (face)
            {
                Rect(p, 1, 17, 30, 12, new("#374e44"));
                Rect(p, 3, 18, 26, 1, new("#7e8b73"));
                Rect(p, 1, 29, 30, 3, Ink);
            }
            if (hash % 3 == 0) Rect(p, 3, face ? 13 : 25, 9, 3, new("#678357"));
        }
        else
        {
            Rect(p, 1, 1, 30, face ? 15 : 30, Metal.Darkened((hash % 3) * 0.07f));
            Rect(p, 2, 2, 28, 2, new("#6c7168"));
            Rect(p, 4, 5, 2, 2, Brass);
            Rect(p, 26, 5, 2, 2, Brass);
            if (face)
            {
                Rect(p, 1, 16, 30, 14, new("#2b3236"));
                Rect(p, 4, 20, 24, 5, new("#4b514b"));
                Rect(p, 9, 20, 2, 5, Ink);
                Rect(p, 20, 20, 2, 5, Ink);
            }
        }
    }

    private void DrawFloorDetail(int x, int y)
    {
        uint hash = WorldData.Hash(x, y, _world.Seed);
        Vector2 p = new(x * 32, y * 32);
        if (Crypt)
        {
            if (hash % 7 == 0)
            {
                Rect(p, 4, 22, 12, 2, new("#526a52", 0.7f));
                Rect(p, 7, 20, 2, 5, new("#789568", 0.45f));
            }
            if (hash % 13 == 0) DrawLine(p + new Vector2(19, 8), p + new Vector2(26, 20), new Color("#354d43"), 1);
        }
        else
        {
            Rect(p, 1, 29, 30, 2, new("#293439", 0.7f));
            Rect(p, 1, 1, 1, 28, new("#293439", 0.6f));
            if (hash % 5 == 0)
            {
                Rect(p, 5, 5, 2, 2, new("#858778"));
                Rect(p, 25, 25, 2, 2, new("#858778"));
            }
        }
    }

    private void DrawCryptProps()
    {
        DrawSupplyLine(new Vector2I[] { new(15, 6), new(15, 22), new(38, 22) }, _state.Activated.Contains("crypt_supply_a"));
        DrawSupplyLine(new Vector2I[] { new(15, 37), new(15, 23), new(38, 23) }, _state.Activated.Contains("crypt_supply_b"));
        foreach (Vector2I at in new Vector2I[] { new(10, 18), new(11, 5), new(21, 12), new(10, 33), new(22, 40), new(26, 4), new(35, 16), new(35, 28), new(41, 16), new(52, 29) })
            if (Near(at, 30)) DrawTorch(WorldData.Center(at));
        if (Near(new(30, 7)))
        {
            Vector2 p = WorldData.Center(new(30, 5));
            Rect(p, -30, -17, 60, 35, Ink);
            Rect(p, -27, -18, 54, 29, new("#718074"));
            Rect(p, -22, -14, 44, 21, new("#586b5a"));
            Rect(p, -2, -9, 4, 14, new("#b4b99b"));
            Rect(p, -8, -5, 16, 3, new("#b4b99b"));
        }
        foreach (Vector2I root in new Vector2I[] { new(25, 16), new(33, 29), new(41, 13), new(52, 31) })
        {
            if (!Near(root, 40)) continue;
            Vector2 p = WorldData.Center(root);
            DrawPolyline(new Vector2[] { p + new Vector2(-15, -12), p, p + new Vector2(14, 9), p + new Vector2(22, 23) }, new Color("#3c4935"), 5);
            DrawLine(p, p + new Vector2(-11, 24), new Color("#4e5e3d"), 3);
        }
    }

    private void DrawForgeProps()
    {
        bool cooled = _state.Activated.Contains("forge_coolant");
        DrawSupplyLine(new Vector2I[] { new(22, 7), new(25, 7), new(25, 17), new(32, 17), new(32, 22), new(38, 22) }, cooled);
        foreach (Vector2I at in new Vector2I[] { new(14, 6), new(14, 12), new(26, 6), new(26, 13), new(12, 32), new(25, 38), new(42, 14), new(51, 14), new(42, 30), new(51, 30) })
            if (Near(at, 70)) DrawMachine(WorldData.Center(at), cooled);
        if (_view.Intersects(DungeonLayout.HeatFloor))
        {
            bool warning = !cooled && DungeonController.HeatWarning(_state.PlayedSeconds);
            bool hot = !cooled && DungeonController.HeatActive(_state.PlayedSeconds);
            Color color = cooled ? new("#689b91") : hot ? new("#ef7651") : warning ? new("#d7a15b") : new("#8c6650");
            for (int y = DungeonLayout.HeatTiles.Position.Y; y < DungeonLayout.HeatTiles.End.Y; y++)
            for (int x = DungeonLayout.HeatTiles.Position.X; x < DungeonLayout.HeatTiles.End.X; x++)
            {
                Vector2 p = new(x * 32, y * 32);
                Rect(p, 2, 2, 28, 28, new Color("#393d3b"));
                for (int line = 0; line < 4; line++) Rect(p, 6, 5 + line * 6, 20, 2, color);
                if (warning || hot) Rect(p, 2, 2, 28, 28, new Color(color, hot ? 0.26f : 0.06f + Mathf.Abs(Mathf.Sin(Clock * 12)) * 0.17f));
            }
            DrawRect(DungeonLayout.HeatFloor, color, false, warning || hot ? 3 : 1);
        }
        foreach (Vector2I at in new Vector2I[] { new(13, 31), new(17, 38), new(25, 31) })
        {
            if (!Near(at, 50)) continue;
            Vector2 p = WorldData.Center(at);
            Rect(p, -15, -13, 30, 28, new("#394642"));
            Rect(p, -12, -14, 24, 23, new("#8c7856"));
            Rect(p, -10, -12, 3, 21, Brass);
            Rect(p, 7, -12, 3, 21, Brass);
        }
    }

    private void DrawSupplyLine(Vector2I[] tiles, bool active)
    {
        var points = new Vector2[tiles.Length];
        for (int i = 0; i < tiles.Length; i++) points[i] = WorldData.Center(tiles[i]);
        DrawPolyline(points, Crypt ? new Color("#354f43") : new Color("#5d635a"), 6);
        DrawPolyline(points, active ? Light : Crypt ? new Color("#627656") : Brass.Darkened(0.35f), 2);
    }

    private void DrawGate()
    {
        if (!Near(new(38, 22), 140)) return;
        Vector2 top = new(38 * 32, 20 * 32);
        bool open = _state.Activated.Contains($"{_world.Area}_gate");
        Color trim = Crypt ? Stone : Metal;
        Rect(top, -5, -9, 42, 9, trim);
        Rect(top, -5, 160, 42, 9, trim);
        if (open)
        {
            Rect(top, 10, -6, 12, 3, Light);
            Rect(top, 10, 163, 12, 3, Light);
            return;
        }
        Rect(top, 0, 0, 32, 160, Ink);
        for (int y = 0; y < 5; y++)
        {
            Rect(top, 2, y * 32 + 2, 28, 29, trim);
            Rect(top, 4, y * 32 + 3, 24, 2, Crypt ? new("#7d8b73") : Brass);
            if (!Crypt) for (int x = 6; x < 29; x += 8) Rect(top, x, y * 32 + 7, 3, 18, Ink);
        }
        if (Crypt)
        {
            DrawCircle(top + new Vector2(16, 61), 5, _state.Activated.Contains("crypt_supply_a") ? Light : new("#273d35"));
            DrawCircle(top + new Vector2(16, 99), 5, _state.Activated.Contains("crypt_supply_b") ? Light : new("#273d35"));
        }
        else Rect(top, 12, 64, 8, 32, Fire);
    }

    private void DrawArena()
    {
        Vector2 at = WorldData.Center(new(46, 22));
        if (!_view.Grow(200).HasPoint(at)) return;
        Ring(at, 145, Crypt ? new("#718575", 0.65f) : new("#b89b62", 0.65f), 3);
        Ring(at, 124, Crypt ? new("#4d6758", 0.6f) : new("#646757", 0.65f), 1);
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.Tau / 8;
            Vector2 point = at + Vector2.FromAngle(angle) * 136;
            DrawLine(point - Vector2.FromAngle(angle) * 5, point + Vector2.FromAngle(angle) * 5, Crypt ? Stone : Brass, 4);
        }
        if (Crypt)
        {
            DrawPolyline(new Vector2[] { at + new Vector2(-35, 30), at + new Vector2(-10, 9), at + new Vector2(0, -34), at + new Vector2(10, 9), at + new Vector2(35, 30) }, new Color("#4b6551"), 5);
            DrawLine(at + new Vector2(0, -7), at + new Vector2(-30, -25), new Color("#4b6551"), 4);
            DrawLine(at + new Vector2(0, -7), at + new Vector2(30, -25), new Color("#4b6551"), 4);
        }
        else
        {
            Ring(at, 41, Brass.Darkened(0.25f), 5);
            for (int i = 0; i < 8; i++)
            {
                Vector2 dir = Vector2.FromAngle(i * Mathf.Tau / 8);
                DrawLine(at + dir * 35, at + dir * 50, Brass.Darkened(0.25f), 8);
            }
        }
    }

    private void DrawSwitch(Vector2 p, bool active)
    {
        DrawCircle(p + new Vector2(0, 11), 17, new Color(Ink, 0.6f));
        Rect(p, -15, 0, 30, 16, Crypt ? Stone.Darkened(0.18f) : Metal);
        Rect(p, -12, -5, 24, 12, Crypt ? Stone : Brass.Darkened(0.35f));
        if (Crypt)
        {
            Ring(p + new Vector2(0, -6), 11, active ? Light : new("#88947d"), 2);
            Rect(p, -4, -16, 8, 17, active ? Light : new("#405947"));
        }
        else
        {
            Vector2 axle = p + new Vector2(0, -9);
            Ring(axle, 14, active ? Light : Brass, 3);
            DrawLine(axle + new Vector2(-9, active ? 9 : -9), axle + new Vector2(9, active ? -9 : 9), active ? Light : Brass, 3);
            Rect(p, -7, 8, 14, 2, active ? Light : Fire);
        }
        if (active) Ring(p, 23 + MathF.Sin(Clock * 2) * 2, new Color(Light, 0.17f), 1);
    }

    private void DrawChest(Vector2 p, bool open)
    {
        Rect(p, -16, 8, 32, 10, new Color(Ink, 0.6f));
        Rect(p, -14, -6, 28, 23, new("#77674d"));
        Rect(p, -11, -3, 22, 17, new("#a49463"));
        Rect(p, -14, open ? -15 : -10, 28, 8, open ? new("#6e785f") : new("#b7a575"));
        Rect(p, -9, -5, 3, 20, Brass);
        Rect(p, 6, -5, 3, 20, Brass);
        Rect(p, -3, 2, 6, 5, open ? Ink : new("#ead295"));
        if (!open) Ring(p, 23, new Color(Brass, 0.13f + MathF.Sin(Clock * 2) * 0.04f));
    }

    private void DrawExit(Vector2 p)
    {
        Rect(p, -20, 10, 40, 9, Ink);
        Rect(p, -17, -24, 8, 34, Crypt ? Stone : Metal);
        Rect(p, 9, -24, 8, 34, Crypt ? Stone : Metal);
        Rect(p, -17, -29, 34, 8, Crypt ? Stone.Lightened(0.1f) : Brass.Darkened(0.15f));
        Rect(p, -8, -17, 16, 25, new Color(Light, 0.18f));
        DrawLine(p + new Vector2(0, 6), p + new Vector2(0, -10), Light, 2);
        DrawLine(p + new Vector2(0, -10), p + new Vector2(-5, -4), Light, 2);
        DrawLine(p + new Vector2(0, -10), p + new Vector2(5, -4), Light, 2);
    }

    private void DrawPlaque(Vector2 p)
    {
        Rect(p, -13, -16, 26, 32, Crypt ? Stone : Metal);
        Rect(p, -10, -13, 20, 23, new("#859181"));
        for (int y = -8; y <= 4; y += 4) Rect(p, -6, y, y % 8 == 0 ? 9 : 13, 1, Ink);
    }

    private void DrawTorch(Vector2 p)
    {
        DrawCircle(p, 25, new Color(Light, 0.045f));
        Rect(p, -4, -3, 8, 17, new("#3e4a35"));
        Rect(p, -6, -8, 12, 6, new("#7d886a"));
        Rect(p, -3, -16 + MathF.Sin(Clock * 5 + p.X) * 2, 6, 10, Light);
        Rect(p, -1, -14, 2, 7, new("#dbefd0"));
    }

    private void DrawMachine(Vector2 p, bool cooled)
    {
        Rect(p, -15, -13, 30, 28, Metal.Darkened(0.25f));
        Rect(p, -14, -18, 28, 24, Metal);
        Rect(p, -10, -16, 20, 2, Brass);
        Rect(p, -8, -10, 16, 10, Ink);
        for (int x = -6; x < 7; x += 4) Rect(p, x, -8, 2, 6, cooled ? Light : Fire);
        Rect(p, -17, -10, 4, 21, Brass.Darkened(0.25f));
        Rect(p, 13, -10, 4, 21, Brass.Darkened(0.25f));
    }
}
