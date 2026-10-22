using Godot;
using System.Linq;

namespace ProjectUnknown;

public static class Wetland
{
    public static readonly Rect2I Basin = new(161, 121, 22, 18);
    public static readonly Vector2I[] Valves = { new(177, 100), new(200, 130) };

    public static void Build(WorldData world)
    {
        for (int y = Basin.Position.Y; y < Basin.End.Y; y++)
        for (int x = Basin.Position.X; x < Basin.End.X; x++)
        {
            bool platform = x >= 165 && x <= 178 && y >= 125 && y <= 134;
            world.Tiles[x, y] = platform ? Terrain.Ruins : Terrain.Water;
            world.Blocked[x, y] = !platform;
        }
        var perimeter = Basin.Grow(2);
        for (int y = perimeter.Position.Y; y < perimeter.End.Y; y++)
        for (int x = perimeter.Position.X; x < perimeter.End.X; x++)
            if (!Basin.HasPoint(new(x, y)))
            {
                world.Tiles[x, y] = Terrain.Path;
                world.Blocked[x, y] = false;
            }
        world.Path(new(151, 116), new(158, 116), new(158, 141), new(186, 141), new(186, 119), new(158, 119));
        world.Path(new(186, 141), new(190, 145));
        world.Objects.AddRange(new WorldObject[]
        {
            new("marsh_pump", "notice", "排水泵站", new(158, 122)),
            new("marsh_west", "valve", "村落压力阀", Valves[0]),
            new("marsh_east", "valve", "水渠泄水阀", Valves[1]),
            new("marsh_supply", "chest", "泵站弓箱", new(156, 125), "reed_bow"),
            new("marsh_vault", "chest", "沉水库房", new(170, 128), "marsh_charm")
        });
        world.Landmarks.Add(new("marsh_pump", "湿地排水站", new(158, 122), 5));
        world.Landmarks.Add(new("marsh_vault", "沉水库房", new(171, 130), 5));
        foreach (var obj in world.Objects.Where(o => o.Id.StartsWith("marsh_") && o.Id != "marsh_vault"))
        {
            world.ClearDisc(obj.Tile, 1, Terrain.Ruins);
            world.ConnectToRoad(obj.Tile);
        }
        world.EnemySpawns.Add(new("marsh_keeper", "elite", new(174, 131), 4));
    }

    public static void Apply(WorldData world, GameState state)
    {
        if (world.Area != "world" || !state.Activated.Contains("marsh_drained")) return;
        for (int y = Basin.Position.Y; y < Basin.End.Y; y++)
        for (int x = Basin.Position.X; x < Basin.End.X; x++)
        {
            world.Tiles[x, y] = Terrain.Ruins;
            world.Blocked[x, y] = false;
        }
    }

    public static bool Interact(Game game, WorldObject obj, out PuzzleFeedback feedback)
    {
        feedback = new("");
        if (game.State.Area != "world" || obj.Kind is not ("valve" or "notice")) return false;
        if (obj.Kind == "notice")
        {
            int pressure = new[] { "marsh_west", "marsh_east" }.Count(game.State.Activated.Contains);
            string clue = game.State.Activated.Contains("marsh_drained") ? "水位已下降。库房的阶梯和通向水渠的近路已经露出。"
                : (pressure == 0 ? "泵站的两块压力表均没有读数。" : "泵站的一块压力表有读数，另一块未响应。")
                    + "管道分别通往沉水村落和断裂水渠。";
            game.Note(clue);
            feedback = new(clue);
            return true;
        }
        if (!game.State.Activated.Add(obj.Id)) { feedback = new("阀门已开启。", true); return true; }
        game.Record("mechanism", obj.Id);
        if (game.State.Activated.Contains("marsh_west") && game.State.Activated.Contains("marsh_east"))
        {
            game.State.Activated.Add("marsh_drained");
            Apply(game.World, game.State);
            game.RefreshTerrain();
            game.Note("湿地排水泵已启动：库房和穿过蓄水池的近路已露出。库房仍有守卫。" );
            feedback = new("排水泵启动。沉水库房和池底近路已开放。", true, true);
        }
        else
        {
            game.Note("已开启" + obj.Name + "。泵站仍缺另一侧压力。");
            feedback = new("阀门开启。一块压力表已亮起，排水泵仍未启动。", true);
        }
        return true;
    }

    public static string Status(GameState state) => state.Activated.Contains("marsh_complete") ? "湿地库房：守卫已击败"
        : state.Activated.Contains("marsh_drained") ? "湿地库房：水位已下降"
        : $"湿地水闸：{new[] { "marsh_west", "marsh_east" }.Count(state.Activated.Contains)} / 2 阀门";
}
