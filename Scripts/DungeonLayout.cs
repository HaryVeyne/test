using Godot;
using System.Collections.Generic;

namespace ProjectUnknown;

public static class DungeonLayout
{
    public static readonly Rect2I HeatTiles = new(27, 19, 8, 7);
    public static Rect2 HeatFloor => new((Vector2)HeatTiles.Position * 32, (Vector2)HeatTiles.Size * 32);
    public static readonly Rect2I BossRoom = new(40, 12, 14, 21);
    public static readonly Rect2I[] CryptRooms =
    {
        new(3, 17, 9, 11), new(10, 3, 12, 11), new(9, 31, 14, 11),
        new(23, 15, 13, 15), new(25, 3, 11, 9), BossRoom
    };
    public static readonly Rect2I[] ForgeRooms =
    {
        new(3, 17, 9, 11), new(13, 4, 15, 11), new(10, 30, 17, 11),
        new(26, 16, 11, 15), BossRoom
    };

    public static void Build(WorldData world)
    {
        for (int y = 0; y < world.Height; y++)
        for (int x = 0; x < world.Width; x++)
        {
            world.Tiles[x, y] = Terrain.Ruins;
            world.Blocked[x, y] = true;
        }
        if (world.Area == "crypt") BuildCrypt(world);
        else BuildForge(world);
        for (int y = 20; y <= 24; y++) world.Blocked[38, y] = true;
        world.Objects.Add(new($"{world.Area}_exit", "exit", "返回地表", new(5, 22), "world"));
        world.Objects.Add(new($"{world.Area}_boss_exit", "exit", "返回地表", new(51, 28), "world"));
    }

    private static void BuildCrypt(WorldData world)
    {
        foreach (Rect2I room in CryptRooms) Carve(world, room, Terrain.Ruins);
        Carve(world, new(6, 20, 35, 5), Terrain.Path);
        Carve(world, new(14, 11, 3, 23), Terrain.Path);
        Carve(world, new(18, 8, 13, 3), Terrain.Path);
        Carve(world, new(28, 8, 3, 29), Terrain.Path);
        Carve(world, new(20, 34, 11, 3), Terrain.Path);
        foreach (Vector2I pillar in new Vector2I[] { new(24, 17), new(34, 17), new(24, 28), new(34, 28), new(42, 14), new(51, 14), new(42, 30), new(51, 30) })
            world.Blocked[pillar.X, pillar.Y] = true;
        world.Landmarks.AddRange(new Landmark[]
        {
            new("crypt_vestibule", "地宫入口", new(6, 22), 4),
            new("crypt_north", "北供能室", new(15, 7), 5),
            new("crypt_south", "南供能室", new(15, 36), 5),
            new("crypt_archive", "石棺侧室", new(30, 7), 4),
            new("crypt_hall", "根须大厅", new(29, 22), 5),
            new("crypt_arena", "守卫墓室", new(46, 22), 6)
        });
        world.Objects.AddRange(new WorldObject[]
        {
            new("crypt_supply_a", "switch", "北供能台", new(15, 6), "crypt_gate"),
            new("crypt_supply_b", "switch", "南供能台", new(15, 37), "crypt_gate"),
            new("crypt_inscription", "shrine", "门锁铭牌", new(8, 18), "两条供能管线分别通向南北侧室。"),
            new("crypt_chest_archive", "chest", "石棺宝箱", new(32, 6), "tide_charm"),
            new("crypt_chest_south", "chest", "守卫物资", new(20, 38), "scale_armor")
        });
        AddEnemies(world, new (string Kind, int X, int Y, int Level)[]
        {
            ("scavenger", 14, 19, 2), ("archer", 20, 11, 2),
            ("guardian", 17, 7, 2), ("spore", 12, 9, 2),
            ("wolf", 20, 33, 2), ("guardian", 12, 36, 3),
            ("archer", 21, 36, 2), ("spore", 28, 28, 2),
            ("scavenger", 26, 18, 2), ("archer", 34, 25, 3),
            ("elite", 30, 6, 3), ("wolf", 27, 10, 2),
            ("guardian", 34, 22, 3)
        });
        world.EnemySpawns.Add(new("crypt_boss", "root_boss", new(46, 22), 3));
    }

    private static void BuildForge(WorldData world)
    {
        foreach (Rect2I room in ForgeRooms) Carve(world, room, Terrain.Ruins);
        Carve(world, new(6, 20, 35, 5), Terrain.Path);
        Carve(world, new(8, 9, 3, 14), Terrain.Path);
        Carve(world, new(9, 8, 9, 3), Terrain.Path);
        Carve(world, new(24, 11, 3, 12), Terrain.Path);
        Carve(world, new(6, 24, 3, 13), Terrain.Path);
        Carve(world, new(7, 34, 10, 3), Terrain.Path);
        Carve(world, new(21, 24, 3, 12), Terrain.Path);
        Carve(world, new(25, 28, 6, 5), Terrain.Path);
        foreach (Vector2I machinery in new Vector2I[] { new(14, 6), new(14, 12), new(26, 6), new(26, 13), new(12, 32), new(25, 38), new(42, 14), new(51, 14), new(42, 30), new(51, 30) })
            world.Blocked[machinery.X, machinery.Y] = true;
        world.Landmarks.AddRange(new Landmark[]
        {
            new("forge_vestibule", "铸炉入口", new(6, 22), 4),
            new("forge_cooling", "冷却车间", new(21, 8), 5),
            new("forge_cargo", "废弃货仓", new(18, 35), 6),
            new("forge_furnace", "热风炉道", new(30, 22), 5),
            new("forge_arena", "核心机房", new(46, 22), 6)
        });
        world.Objects.AddRange(new WorldObject[]
        {
            new("forge_coolant", "switch", "冷却阀", new(22, 7), "forge_gate"),
            new("forge_inscription", "shrine", "维护铭牌", new(8, 18), "冷却管通向炉道与东侧门锁。红色地板升温前会闪烁。"),
            new("forge_chest_cargo", "chest", "货仓宝箱", new(23, 36), "frost_armor"),
            new("forge_chest_cooling", "chest", "维修储物箱", new(17, 6), "copper_ring")
        });
        AddEnemies(world, new (string Kind, int X, int Y, int Level)[]
        {
            ("guardian", 14, 22, 4), ("archer", 18, 13, 4),
            ("scavenger", 15, 9, 4), ("spore", 25, 8, 4),
            ("wolf", 25, 19, 4), ("archer", 23, 29, 4),
            ("guardian", 14, 34, 4), ("elite", 21, 35, 5),
            ("scavenger", 19, 32, 4), ("spore", 29, 28, 4),
            ("guardian", 34, 18, 5), ("archer", 34, 26, 4),
            ("wolf", 33, 22, 4)
        });
        world.EnemySpawns.Add(new("forge_boss", "forge_boss", new(46, 22), 5));
    }

    private static void Carve(WorldData world, Rect2I rect, Terrain floor)
    {
        for (int y = rect.Position.Y; y < rect.End.Y; y++)
        for (int x = rect.Position.X; x < rect.End.X; x++)
        {
            world.Tiles[x, y] = floor;
            world.Blocked[x, y] = false;
        }
    }

    private static void AddEnemies(WorldData world, IEnumerable<(string Kind, int X, int Y, int Level)> enemies)
    {
        int index = 0;
        foreach (var spawn in enemies)
            world.EnemySpawns.Add(new($"{world.Area}_{index++}", spawn.Kind, new(spawn.X, spawn.Y), spawn.Level));
    }

    public static void OpenGate(WorldData world, string id)
    {
        if (id != $"{world.Area}_gate") return;
        for (int y = 20; y <= 24; y++)
        {
            world.Blocked[38, y] = false;
            world.Tiles[38, y] = Terrain.Path;
        }
    }
}
