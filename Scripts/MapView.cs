using Godot;

namespace ProjectUnknown;

public partial class MapView : Control
{
    private WorldData _world = null!;
    private GameState _state = null!;
    private Font _font = null!;
    private Vector2 _position;
    private Vector2 _heading = Vector2.Up;

    private static readonly Color Ink = new("101d23");
    private static readonly Color Border = new("46585a");
    private static readonly Color Ivory = new("e5e2ce");
    private static readonly Color Gold = new("e8c98a");
    private static readonly Color Cyan = new("7dd8cc");

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(650, 570);
        MouseFilter = MouseFilterEnum.Ignore;
        var fontPath = ResourceLoader.Exists("res://Assets/Fonts/GameFont.otf")
            ? "res://Assets/Fonts/GameFont.otf"
            : "res://Assets/Fonts/NotoSansSC-Regular.otf";
        _font = ResourceLoader.Exists(fontPath) ? GD.Load<Font>(fontPath) : ThemeDB.FallbackFont;
        Resized += QueueRedraw;
    }

    public void Configure(WorldData world, GameState state)
    {
        _world = world;
        _state = state;
        _position = new Vector2(state.PlayerX, state.PlayerY);
        QueueRedraw();
    }

    public void Refresh(Vector2 position)
    {
        if (position.DistanceSquaredTo(_position) > 1)
            _heading = (position - _position).Normalized();
        _position = position;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_world is null)
            return;

        float edge = Mathf.Min(Size.X - 70, Size.Y - 48);
        var origin = new Vector2((Size.X - edge) * .5f, 20);
        var bounds = new Rect2(origin, new Vector2(edge, edge));
        float cell = edge / WorldData.Size;

        DrawRect(new Rect2(origin - Vector2.One * 5, bounds.Size + Vector2.One * 10), new Color("0c171c"));
        DrawRect(bounds, Ink);
        for (int y = 0; y < WorldData.Size; y++)
        for (int x = 0; x < WorldData.Size; x++)
        {
            if (!_state.Explored.Contains(x + y * WorldData.Size))
                continue;
            Color color = TerrainColor(_world.TileAt(x, y));
            float shade = 1 + (((x * 7 + y * 13) % 5) - 2) * .015f;
            color = new Color(color.R * shade, color.G * shade, color.B * shade);
            DrawRect(new Rect2(origin + new Vector2(x, y) * cell, Vector2.One * (cell + .2f)), color);
        }

        for (int grid = 0; grid <= WorldData.Size; grid += 20)
        {
            float offset = grid * cell;
            var gridColor = new Color(.8f, .9f, .8f, .055f);
            DrawLine(origin + new Vector2(offset, 0), origin + new Vector2(offset, edge), gridColor);
            DrawLine(origin + new Vector2(0, offset), origin + new Vector2(edge, offset), gridColor);
        }
        DrawRect(bounds, Border, false, 1);
        DrawCorners(bounds);

        foreach (var landmark in _world.Landmarks)
        {
            if (!_state.Discovered.Contains(landmark.Id))
                continue;
            var point = origin + (new Vector2(landmark.Tile.X, landmark.Tile.Y) + Vector2.One * .5f) * cell;
            bool activated = _state.Activated.Contains(landmark.Id);
            Color accent = activated ? Cyan : Gold;
            if (landmark.Id == "center")
            {
                DrawPolyline(new[] { point + Vector2.Up * 7, point + Vector2.Right * 7,
                    point + Vector2.Down * 7, point + Vector2.Left * 7, point + Vector2.Up * 7 }, accent, 2, true);
            }
            else
            {
                DrawCircle(point, 4, Ink);
                DrawArc(point, 5, 0, Mathf.Tau, 20, accent, 1.5f, true);
            }
            DrawCircle(point, 1.5f, accent);
            DrawLandmarkName(landmark.Name, point + Vector2.Down * 16, bounds, accent);
        }

        var player = origin + _position / WorldData.TileSize * cell;
        player.X = Mathf.Clamp(player.X, bounds.Position.X + 2, bounds.End.X - 2);
        player.Y = Mathf.Clamp(player.Y, bounds.Position.Y + 2, bounds.End.Y - 2);
        DrawCircle(player, 7, new Color("101d23"));
        DrawArc(player, 8, 0, Mathf.Tau, 24, new Color(1, 1, 1, .4f), 1, true);
        var side = new Vector2(-_heading.Y, _heading.X);
        DrawColoredPolygon(new[] { player + _heading * 7, player - _heading * 4 + side * 4,
            player - _heading * 2, player - _heading * 4 - side * 4 }, Ivory);

        DrawCompass(new Vector2(bounds.End.X + 21, bounds.Position.Y + 42));
        DrawString(_font, new Vector2(bounds.Position.X, bounds.End.Y + 22), "已绘制的世界", HorizontalAlignment.Left, -1, 12, new Color("879a99"));
        string progress = $"{_state.Explored.Count * 100f / (WorldData.Size * WorldData.Size):0.0}%";
        var progressSize = _font.GetStringSize(progress, HorizontalAlignment.Left, -1, 12);
        DrawString(_font, new Vector2(bounds.End.X - progressSize.X, bounds.End.Y + 22), progress, HorizontalAlignment.Left, -1, 12, Gold);
    }

    private void DrawLandmarkName(string name, Vector2 position, Rect2 bounds, Color accent)
    {
        var textSize = _font.GetStringSize(name, HorizontalAlignment.Left, -1, 12);
        float x = Mathf.Clamp(position.X - textSize.X / 2, bounds.Position.X + 5, bounds.End.X - textSize.X - 5);
        float y = Mathf.Min(position.Y, bounds.End.Y - 6);
        DrawRect(new Rect2(new Vector2(x - 4, y - 13), new Vector2(textSize.X + 8, 18)), new Color(.035f, .07f, .085f, .9f));
        DrawString(_font, new Vector2(x, y), name, HorizontalAlignment.Left, -1, 12, accent);
    }

    private void DrawCorners(Rect2 bounds)
    {
        foreach (var corner in new[] { bounds.Position, new Vector2(bounds.End.X, bounds.Position.Y),
            bounds.End, new Vector2(bounds.Position.X, bounds.End.Y) })
        {
            Vector2 direction = new(corner.X == bounds.Position.X ? 1 : -1, corner.Y == bounds.Position.Y ? 1 : -1);
            DrawLine(corner, corner + Vector2.Right * direction.X * 14, Gold, 2);
            DrawLine(corner, corner + Vector2.Down * direction.Y * 14, Gold, 2);
        }
    }

    private void DrawCompass(Vector2 point)
    {
        DrawLine(point + Vector2.Up * 14, point + Vector2.Down * 9, Border);
        DrawLine(point + Vector2.Left * 7, point + Vector2.Right * 7, Border);
        DrawColoredPolygon(new[] { point + Vector2.Up * 14, point + new Vector2(4, 1), point + new Vector2(-4, 1) }, Gold);
        DrawString(_font, point + new Vector2(-4, -19), "N", HorizontalAlignment.Left, -1, 11, Ivory);
    }

    private static Color TerrainColor(Terrain terrain) => terrain switch
    {
        Terrain.Meadow => new Color("657d58"),
        Terrain.Forest => new Color("344f42"),
        Terrain.Dunes => new Color("b49c70"),
        Terrain.Water => new Color("345c6b"),
        Terrain.Ruins => new Color("7b8481"),
        Terrain.Path => new Color("949073"),
        _ => Ink
    };
}
