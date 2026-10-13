using Godot;
using System.Collections.Generic;
using System.Linq;

namespace ProjectUnknown;

public partial class MapView : Control
{
    private WorldData _world = null!;
    private GameState _state = null!;
    private Font _font = null!;
    private Vector2 _position, _pan, _mouse = new(-100, -100);
    private Vector2 _heading = Vector2.Up;
    private float _zoom = 1;
    private bool _dragging;
    private static readonly Color Ink = new("101d23"), Border = new("46585a"), Ivory = new("e5e2ce"), Gold = new("e8c98a"), Cyan = new("7dd8cc");

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(650, 388);
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
        const string path = "res://Assets/Fonts/NotoSansSC-Regular.otf";
        _font = ResourceLoader.Exists(path) ? GD.Load<Font>(path) : ThemeDB.FallbackFont;
        Resized += QueueRedraw;
        MouseExited += () => { _mouse = new Vector2(-100, -100); _dragging = false; QueueRedraw(); };
    }

    public void Configure(WorldData world, GameState state)
    {
        if (_world != world) { _pan = Vector2.Zero; _zoom = 1; }
        _world = world; _state = state;
        _position = new Vector2(state.PlayerX, state.PlayerY);
        QueueRedraw();
    }
    public void Refresh(Vector2 position)
    {
        if (position.DistanceSquaredTo(_position) > 1) _heading = (position - _position).Normalized();
        _position = position; QueueRedraw();
    }
    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventMouseButton button)
        {
            if (button.ButtonIndex == MouseButton.Left) _dragging = button.Pressed;
            if (button.Pressed && button.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                float previous = _zoom;
                _zoom = Mathf.Clamp(_zoom * (button.ButtonIndex == MouseButton.WheelUp ? 1.25f : .8f), 1, 4);
                Vector2 center = Size * .5f;
                _pan = (_pan + center - button.Position) * (_zoom / previous) - center + button.Position;
                if (_zoom == 1) _pan = Vector2.Zero;
                AcceptEvent();
            }
        }
        if (input is InputEventMouseMotion motion)
        {
            _mouse = motion.Position;
            if (_dragging && _zoom > 1) _pan += motion.Relative;
        }
        QueueRedraw();
    }
    public override void _Draw()
    {
        if (_world is null) return;
        float cell = Mathf.Min((Size.X - 70) / _world.Width, (Size.Y - 50) / _world.Height) * _zoom;
        var dimensions = new Vector2(_world.Width * cell, _world.Height * cell);
        var origin = new Vector2((Size.X - dimensions.X) * .5f, (Size.Y - 26 - dimensions.Y) * .5f) + _pan;
        var bounds = new Rect2(origin, dimensions);
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("0c171c"));
        DrawRect(bounds, Ink);
        var explored = _state.ExploredFor(_world.Area);
        for (int y = 0; y < _world.Height; y++)
        for (int x = 0; x < _world.Width; x++)
        {
            if (!explored.Contains(x + y * _world.Width)) continue;
            var tileBounds = new Rect2(origin + new Vector2(x, y) * cell, Vector2.One * (cell + .2f));
            if (!tileBounds.Intersects(new Rect2(Vector2.Zero, Size))) continue;
            Color color = TerrainColor(_world.TileAt(x, y));
            float shade = 1 + (((x * 7 + y * 13) % 5) - 2) * .015f;
            DrawRect(tileBounds, new Color(color.R * shade, color.G * shade, color.B * shade));
        }
        int step = _world.Area == "world" ? 20 : 8;
        var gridColor = new Color(.8f, .9f, .8f, .055f);
        for (int x = 0; x <= _world.Width; x += step) DrawLine(origin + new Vector2(x * cell, 0), origin + new Vector2(x * cell, dimensions.Y), gridColor);
        for (int y = 0; y <= _world.Height; y += step) DrawLine(origin + new Vector2(0, y * cell), origin + new Vector2(dimensions.X, y * cell), gridColor);
        DrawRect(bounds, Border, false, 1);
        DrawCorners(bounds);

        var labelBounds = new List<Rect2>();
        string hoverName = "";
        foreach (var landmark in _world.Landmarks.OrderBy(site => site.Id == "center" ? 0 : 1))
        {
            if (!_state.Discovered.Contains(landmark.Id)) continue;
            Vector2 point = origin + ((Vector2)landmark.Tile + Vector2.One * .5f) * cell;
            if (!new Rect2(Vector2.Zero, Size).HasPoint(point)) continue;
            bool activated = _state.Activated.Contains(landmark.Id) || _state.Checkpoints.Contains(landmark.Id);
            Color accent = activated ? Cyan : Gold;
            DrawMarker(point, accent, landmark.Id == "center");
            if (point.DistanceSquaredTo(_mouse) < 14 * 14) hoverName = landmark.Name;
            if (_zoom >= 1.8f || landmark.Id == "center" || landmark.Id == "camp" || landmark.Id.EndsWith("entry"))
                DrawLandmarkName(landmark.Name, point + Vector2.Down * 16, bounds, accent, labelBounds);
        }
        foreach (var obj in _world.Objects)
        {
            if (!explored.Contains(obj.Tile.X + obj.Tile.Y * _world.Width)) continue;
            Vector2 point = origin + ((Vector2)obj.Tile + Vector2.One * .5f) * cell;
            if (!new Rect2(Vector2.Zero, Size).HasPoint(point)) continue;
            if (obj.Kind == "checkpoint")
            {
                bool unlocked = _state.Checkpoints.Contains(obj.Id);
                DrawRect(new Rect2(point - Vector2.One * 3, Vector2.One * 6), unlocked ? Cyan : Gold);
                if (_zoom >= 1.8f) DrawLandmarkName(obj.Name, point + Vector2.Down * 16, bounds, unlocked ? Cyan : Gold, labelBounds);
            }
            else if (obj.Kind is "portal" or "exit") DrawArc(point, 6, 0, Mathf.Tau, 16, Cyan, 2, true);
            else if (obj.Kind == "chest" && !_state.OpenedChests.Contains(obj.Id)) DrawRect(new Rect2(point - new Vector2(2, 2), new Vector2(4, 4)), Gold);
            else if (obj.Kind == "switch") DrawCircle(point, 3, _state.Activated.Contains(obj.Id) ? Cyan : Gold);
            if (point.DistanceSquaredTo(_mouse) < 12 * 12) hoverName = obj.Name;
        }
        var player = origin + _position / WorldData.TileSize * cell;
        DrawCircle(player, 7, Ink); DrawArc(player, 8, 0, Mathf.Tau, 24, new Color(1, 1, 1, .4f), 1, true);
        var side = new Vector2(-_heading.Y, _heading.X);
        DrawColoredPolygon(new[] { player + _heading * 7, player - _heading * 4 + side * 4, player - _heading * 2, player - _heading * 4 - side * 4 }, Ivory);
        DrawCompass(new Vector2(Size.X - 22, 36));
        DrawRect(new Rect2(0, Size.Y - 25, Size.X, 25), new Color("0c171c"));
        string status = hoverName.Length > 0 ? hoverName : "滚轮缩放 · 放大后拖动 · 指向标记查看地点";
        DrawString(_font, new Vector2(12, Size.Y - 8), status, HorizontalAlignment.Left, -1, 13, hoverName.Length > 0 ? Gold : new Color("879a99"));
        string progress = $"{explored.Count * 100f / (_world.Width * _world.Height):0.0}%";
        DrawString(_font, new Vector2(Size.X - 65, Size.Y - 8), progress, HorizontalAlignment.Left, -1, 13, Gold);
    }
    private void DrawMarker(Vector2 point, Color accent, bool central)
    {
        if (central) DrawPolyline(new[] { point + Vector2.Up * 7, point + Vector2.Right * 7, point + Vector2.Down * 7, point + Vector2.Left * 7, point + Vector2.Up * 7 }, accent, 2, true);
        else { DrawCircle(point, 4, Ink); DrawArc(point, 5, 0, Mathf.Tau, 20, accent, 1.5f, true); }
        DrawCircle(point, 1.5f, accent);
    }
    private void DrawLandmarkName(string name, Vector2 position, Rect2 bounds, Color accent, List<Rect2> used)
    {
        var textSize = _font.GetStringSize(name, HorizontalAlignment.Left, -1, 12);
        float x = Mathf.Clamp(position.X - textSize.X / 2, Mathf.Max(bounds.Position.X, 0) + 5, Mathf.Max(bounds.Position.X + 5, Mathf.Min(bounds.End.X, Size.X) - textSize.X - 5));
        float y = Mathf.Min(position.Y, Mathf.Min(bounds.End.Y, Size.Y - 29) - 6);
        var background = new Rect2(new Vector2(x - 4, y - 13), new Vector2(textSize.X + 8, 18));
        if (used.Any(rect => rect.Intersects(background))) return;
        used.Add(background); DrawRect(background, new Color(.035f, .07f, .085f, .9f));
        DrawString(_font, new Vector2(x, y), name, HorizontalAlignment.Left, -1, 12, accent);
    }
    private void DrawCorners(Rect2 bounds)
    {
        foreach (var corner in new[] { bounds.Position, new Vector2(bounds.End.X, bounds.Position.Y), bounds.End, new Vector2(bounds.Position.X, bounds.End.Y) })
        {
            Vector2 direction = new(corner.X == bounds.Position.X ? 1 : -1, corner.Y == bounds.Position.Y ? 1 : -1);
            DrawLine(corner, corner + Vector2.Right * direction.X * 14, Gold, 2); DrawLine(corner, corner + Vector2.Down * direction.Y * 14, Gold, 2);
        }
    }
    private void DrawCompass(Vector2 point)
    {
        DrawLine(point + Vector2.Up * 14, point + Vector2.Down * 9, Border); DrawLine(point + Vector2.Left * 7, point + Vector2.Right * 7, Border);
        DrawColoredPolygon(new[] { point + Vector2.Up * 14, point + new Vector2(4, 1), point + new Vector2(-4, 1) }, Gold);
        DrawString(_font, point + new Vector2(-4, -19), "N", HorizontalAlignment.Left, -1, 11, Ivory);
    }
    private static Color TerrainColor(Terrain terrain) => terrain switch
    {
        Terrain.Meadow => new Color("657d58"), Terrain.Forest => new Color("344f42"), Terrain.Dunes => new Color("b49c70"),
        Terrain.Water => new Color("345c6b"), Terrain.Ruins => new Color("7b8481"), Terrain.Path => new Color("949073"),
        Terrain.Wetland => new Color("496d63"), Terrain.Snow => new Color("a7bec9"), _ => Ink
    };
}
