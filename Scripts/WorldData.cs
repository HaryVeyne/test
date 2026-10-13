using Godot;
using System;
using System.Collections.Generic;

namespace ProjectUnknown;

public enum Terrain { Meadow, Forest, Dunes, Water, Ruins, Path, Wetland, Snow }
public record EnemySpawn(string Id, string Kind, Vector2I Tile, int Level = 1);
public record WorldObject(string Id, string Kind, string Name, Vector2I Tile, string Value = "");
public record Landmark(string Id, string Name, Vector2I Tile, float Radius = 5);

public sealed class WorldData
{
    public const int Size = 220;
    public const int TileSize = 32;
    public int Seed { get; }
    public string Area { get; }
    public int Width { get; }
    public int Height { get; }
    public Terrain[,] Tiles { get; }
    public bool[,] Blocked { get; }
    public List<EnemySpawn> EnemySpawns { get; } = new();
    public List<WorldObject> Objects { get; } = new();
    public List<Landmark> Landmarks { get; } = new();

    private static readonly Landmark[] OriginalLandmarks =
    {
        new("camp", "旅人营地", new(56, 85), 4),
        new("center", "无名之庭", new(60, 59), 8),
        new("forest", "回声林地", new(27, 32), 6),
        new("dunes", "日轮沙庭", new(94, 43), 6),
        new("lake", "静潮之湖", new(27, 82), 7),
        new("overlook", "风之高地", new(72, 24), 4),
        new("archive", "残页石室", new(89, 91), 4),
        new("hollow", "空心古树", new(12, 54), 4),
        new("bell", "失落钟塔", new(48, 108), 4)
    };
    public Vector2 Spawn => Center(Area == "world" ? new(56, 87) : new(6, 22));

    public WorldData(int seed, string area = "world")
    {
        Seed = seed;
        Area = area;
        Width = area == "world" ? Size : 56;
        Height = area == "world" ? Size : 44;
        Tiles = new Terrain[Width, Height];
        Blocked = new bool[Width, Height];
        if (area != "world")
        {
            DungeonLayout.Build(this);
            return;
        }
        Landmarks.AddRange(OriginalLandmarks);
        Landmarks.AddRange(new Landmark[]
        {
            new("forest_outpost", "北林营地", new(40, 21), 5),
            new("crypt_entry", "林地墓室", new(34, 27), 5),
            new("dunes_outpost", "沙海营地", new(113, 67), 5),
            new("caravan", "废弃商站", new(154, 33), 6),
            new("sunken", "沉水村落", new(177, 95), 7),
            new("wetland_outpost", "湿地营地", new(151, 116), 5),
            new("aqueduct", "断裂水渠", new(202, 132), 6),
            new("forge_entry", "熔炉工坊", new(173, 157), 6),
            new("snow_outpost", "雪线营地", new(179, 179), 5),
            new("observatory", "山顶观测台", new(203, 199), 7),
            new("watchtower", "南境哨塔", new(97, 170), 6),
            new("grove", "长枝林", new(44, 165), 6)
        });
        GenerateTerrain();
        LayPaths();
        PlaceLandmarks();
        BuildLake();
        PlaceAdventureContent();
    }

    public static Vector2 Center(Vector2I tile) => (Vector2)tile * TileSize + Vector2.One * (TileSize / 2);
    public Terrain TileAt(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height ? Tiles[x, y] : Terrain.Water;
    public bool IsBlocked(int x, int y, bool bridgeOpen = false)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return true;
        if (bridgeOpen && x >= 26 && x <= 28 && y >= 81 && y <= 83) return false;
        return Blocked[x, y];
    }

    public void OpenGate(string id) => DungeonLayout.OpenGate(this, id);

    public void OpenBridge()
    {
        for (int x = 26; x <= 28; x++)
        for (int y = 81; y <= 83; y++)
        {
            Tiles[x, y] = Terrain.Ruins;
            Blocked[x, y] = false;
        }
    }

    private void GenerateTerrain()
    {
        for (int x = 0; x < Size; x++)
        for (int y = 0; y < Size; y++)
        {
            float roughness = Noise(x / 9f, y / 9f) * 5;
            float forest = Mathf.Pow((x - 27) / 32f, 2) + Mathf.Pow((y - 34) / 35f, 2);
            float dunes = Mathf.Pow((x - 101) / 29f, 2) + Mathf.Pow((y - 38) / 36f, 2);
            Terrain terrain = forest < 0.84f + roughness * 0.04f ? Terrain.Forest : Terrain.Meadow;
            if (dunes < 1.02f + roughness * 0.025f) terrain = Terrain.Dunes;
            float northForest = Mathf.Pow((x - 56) / 47f, 2) + Mathf.Pow((y - 20) / 24f, 2);
            float southForest = Mathf.Pow((x - 42) / 28f, 2) + Mathf.Pow((y - 165) / 37f, 2);
            float eastDunes = Mathf.Pow((x - 151) / 70f, 2) + Mathf.Pow((y - 43) / 45f, 2);
            float marsh = Mathf.Pow((x - 179) / 56f, 2) + Mathf.Pow((y - 112) / 42f, 2);
            float snow = Mathf.Pow((x - 183) / 68f, 2) + Mathf.Pow((y - 184) / 54f, 2);
            if (northForest < 0.9f + roughness * 0.035f || southForest < 0.9f) terrain = Terrain.Forest;
            if (x > 104 && eastDunes < 1.08f + roughness * 0.025f) terrain = Terrain.Dunes;
            if (x > 120 && marsh < 1.02f + roughness * 0.025f) terrain = Terrain.Wetland;
            if (x > 117 && y > 145 && snow < 1.02f + roughness * 0.045f) terrain = Terrain.Snow;
            Tiles[x, y] = terrain;
            uint h = Hash(x, y, Seed);
            Blocked[x, y] = (terrain == Terrain.Forest && h % 100 < 23)
                || (terrain == Terrain.Dunes && h % 180 == 0)
                || (terrain == Terrain.Wetland && h % 80 == 0)
                || (terrain == Terrain.Snow && h % 30 == 0);
            if (terrain == Terrain.Wetland && Noise(x / 4f, y / 4f) > 0.76f)
            {
                Tiles[x, y] = Terrain.Water;
                Blocked[x, y] = true;
            }
            if (x == 0 || y == 0 || x == Size - 1 || y == Size - 1)
            {
                Tiles[x, y] = Terrain.Water;
                Blocked[x, y] = true;
            }
        }

        for (int y = 88; y < Size; y++)
        {
            float riverX = 38.5f + (y - 88) * 0.22f + MathF.Sin(y * 0.18f) * 1.7f;
            for (int x = (int)riverX - 1; x <= (int)riverX + 1; x++)
            {
                Tiles[x, y] = Terrain.Water;
                Blocked[x, y] = true;
            }
        }
        for (int y = 84; y < 162; y++)
        {
            int riverX = 131 + (int)((y - 84) * 0.26f + MathF.Sin(y * 0.1f) * 5);
            for (int x = riverX - 1; x <= riverX + 2; x++)
            {
                Tiles[x, y] = Terrain.Water;
                Blocked[x, y] = true;
            }
        }
        for (int x = 65; x < 82; x++)
        for (int y = 9; y < 20; y++)
            if (Mathf.Pow((x - 74) / 7.5f, 2) + Mathf.Pow((y - 14) / 4.5f, 2) < 1)
            {
                Tiles[x, y] = Terrain.Water;
                Blocked[x, y] = true;
            }
    }

    private void LayPaths()
    {
        Path(new(56, 87), new(59, 76), new(60, 59));
        Path(new(60, 59), new(46, 51), new(39, 40), new(27, 32));
        Path(new(27, 32), new(19, 40), new(12, 54));
        Path(new(12, 54), new(15, 67), new(20, 75), new(20, 82), new(23, 82));
        Path(new(60, 59), new(49, 65), new(35, 68), new(23, 71), new(20, 78), new(23, 82));
        Path(new(56, 87), new(48, 75), new(35, 68));
        Path(new(60, 59), new(75, 56), new(84, 47), new(94, 43));
        Path(new(94, 43), new(94, 29), new(82, 24), new(72, 24));
        Path(new(72, 24), new(54, 25), new(39, 28), new(27, 32));
        Path(new(72, 24), new(68, 41), new(60, 59));
        Path(new(94, 43), new(103, 60), new(101, 77), new(89, 91));
        Path(new(89, 91), new(75, 90), new(56, 87));
        Path(new(60, 59), new(71, 73), new(89, 91));
        Path(new(56, 87), new(57, 99), new(56, 110), new(48, 108));
        Path(new(48, 108), new(73, 109), new(89, 91));
        Path(new(27, 32), new(34, 27), new(40, 21), new(59, 20), new(72, 24));
        Path(new(94, 43), new(113, 67), new(136, 62), new(154, 33));
        Path(new(94, 43), new(117, 30), new(154, 33), new(181, 47), new(190, 68));
        Path(new(89, 91), new(113, 67), new(128, 80), new(151, 116));
        Path(new(154, 33), new(166, 63), new(177, 95), new(151, 116));
        Path(new(177, 95), new(199, 101), new(202, 132), new(180, 132), new(151, 116));
        Path(new(151, 116), new(154, 140), new(173, 157), new(179, 179));
        Path(new(202, 132), new(203, 160), new(200, 180), new(203, 199));
        Path(new(173, 157), new(194, 154), new(203, 160));
        Path(new(179, 179), new(190, 198), new(203, 199));
        Path(new(48, 108), new(42, 137), new(44, 165), new(72, 168), new(97, 170));
        Path(new(89, 91), new(107, 118), new(97, 145), new(97, 170));
        Path(new(151, 116), new(127, 135), new(97, 145));
        Path(new(97, 170), new(132, 178), new(179, 179));
        Path(new(44, 165), new(62, 190), new(91, 201), new(132, 178));
        Path(new(173, 157), new(147, 159), new(132, 178));
    }

    private void Path(params Vector2I[] points)
    {
        for (int p = 1; p < points.Length; p++)
        {
            Vector2 a = points[p - 1], b = points[p];
            int steps = (int)a.DistanceTo(b) * 3;
            for (int i = 0; i <= steps; i++)
            {
                Vector2 at = a.Lerp(b, (float)i / Math.Max(steps, 1));
                at += new Vector2(MathF.Sin(i * 0.21f + p) * 0.35f, MathF.Sin(i * 0.15f) * 0.35f);
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    int x = Mathf.RoundToInt(at.X) + dx, y = Mathf.RoundToInt(at.Y) + dy;
                    if (x < 2 || y < 2 || x > Size - 3 || y > Size - 3) continue;
                    Tiles[x, y] = Terrain.Path;
                    Blocked[x, y] = false;
                }
            }
        }
    }

    private void PlaceLandmarks()
    {
        foreach (var landmark in Landmarks)
        {
            int radius = landmark.Id == "center" ? 7 : landmark.Id is "forest" or "dunes" ? 5
                : Array.Exists(OriginalLandmarks, old => old.Id == landmark.Id) ? 3 : 5;
            Terrain floor = landmark.Id is "center" or "archive" or "bell" or "caravan" or "sunken"
                or "aqueduct" or "forge_entry" or "crypt_entry" or "observatory" or "watchtower" ? Terrain.Ruins
                : landmark.Id == "dunes" ? Terrain.Dunes
                : landmark.Id == "snow_outpost" ? Terrain.Snow
                : landmark.Id == "wetland_outpost" ? Terrain.Wetland
                : landmark.Id == "dunes_outpost" ? Terrain.Dunes
                : landmark.Id == "grove" ? Terrain.Forest : Terrain.Meadow;
            ClearDisc(landmark.Tile, radius, floor);
        }
        foreach (Vector2I stone in new Vector2I[] { new(24, 32), new(27, 29), new(30, 32) })
            ClearDisc(stone, 2, Terrain.Meadow);
        ClearDisc(new(91, 43), 2, Terrain.Dunes);
        ClearDisc(new(97, 43), 2, Terrain.Dunes);
        // Four ruined corners leave broad entrances along every cardinal direction.
        foreach (Vector2I pillar in new Vector2I[] { new(55, 54), new(65, 54), new(55, 64), new(65, 64) })
            Blocked[pillar.X, pillar.Y] = true;
    }

    private void ClearDisc(Vector2I center, int radius, Terrain terrain)
    {
        for (int x = center.X - radius; x <= center.X + radius; x++)
        for (int y = center.Y - radius; y <= center.Y + radius; y++)
            if (x > 1 && y > 1 && x < Size - 2 && y < Size - 2 && new Vector2(x - center.X, y - center.Y).Length() <= radius)
            {
                Tiles[x, y] = terrain;
                Blocked[x, y] = false;
            }
    }

    private void BuildLake()
    {
        for (int x = 20; x <= 43; x++)
        for (int y = 71; y <= 92; y++)
        {
            float d = Mathf.Pow((x - 32) / 10.5f, 2) + Mathf.Pow((y - 82) / 9.5f, 2);
            if (d < 1 + Noise(x / 3f, y / 3f) * 0.04f)
            {
                Tiles[x, y] = Terrain.Water;
                Blocked[x, y] = true;
            }
        }
        for (int x = 20; x <= 25; x++)
        for (int y = 79; y <= 85; y++)
        {
            Tiles[x, y] = x == 23 ? Terrain.Path : Terrain.Meadow;
            Blocked[x, y] = false;
        }
        for (int x = 29; x <= 34; x++)
        for (int y = 80; y <= 84; y++)
        {
            Tiles[x, y] = Terrain.Ruins;
            Blocked[x, y] = false;
        }
        // The whole island has a water moat. Only this nine-tile causeway can rise.
        for (int x = 26; x <= 28; x++)
        for (int y = 81; y <= 83; y++)
        {
            Tiles[x, y] = Terrain.Water;
            Blocked[x, y] = true;
        }
    }

    private void PlaceAdventureContent()
    {
        Objects.AddRange(new WorldObject[]
        {
            new("camp", "checkpoint", "旅人营地", new(56, 85)),
            new("forest_camp", "checkpoint", "北林营地", new(40, 21)),
            new("dunes_camp", "checkpoint", "沙海营地", new(113, 67)),
            new("wetland_camp", "checkpoint", "湿地营地", new(151, 116)),
            new("snow_camp", "checkpoint", "雪线营地", new(179, 179)),
            new("crypt_entry", "portal", "林地墓室", new(34, 27), "crypt"),
            new("forge_entry", "portal", "熔炉工坊", new(173, 157), "forge"),
            new("old_watch_chest", "chest", "哨塔宝箱", new(76, 26), "copper_ring"),
            new("forest_cache", "chest", "树根藏箱", new(17, 52), "health_potion"),
            new("caravan_chest", "chest", "商站货箱", new(154, 34), "scale_armor"),
            new("dunes_cache", "chest", "沙丘藏箱", new(127, 45), "iron_sword"),
            new("archive_cache", "chest", "石室宝箱", new(92, 93), "health_potion"),
            new("village_chest", "chest", "村落宝箱", new(178, 95), "tide_charm"),
            new("aqueduct_cache", "chest", "水渠藏箱", new(202, 135), "health_potion"),
            new("grove_chest", "chest", "林中藏箱", new(36, 168), "health_potion"),
            new("watchtower_chest", "chest", "南境军械箱", new(97, 171), "scale_armor"),
            new("snow_cache", "chest", "雪山物资箱", new(195, 166), "health_potion"),
            new("observatory_chest", "chest", "观测台宝箱", new(204, 199), "frost_armor"),
            new("southern_cache", "chest", "山道藏箱", new(126, 183), "health_potion")
        });
        foreach (WorldObject obj in Objects)
        {
            Terrain floor = Tiles[obj.Tile.X, obj.Tile.Y];
            if (floor == Terrain.Water) floor = Terrain.Path;
            ClearDisc(obj.Tile, obj.Kind == "checkpoint" ? 4 : 2, floor);
            ConnectToRoad(obj.Tile);
        }

        var groups = new (Vector2I Tile, int Level, string[] Kinds)[]
        {
            (new(74, 94), 1, new[]{ "scavenger", "scavenger", "wolf", "scavenger" }),
            (new(61, 105), 1, new[]{ "scavenger", "wolf", "archer", "scavenger" }),
            (new(73, 77), 1, new[]{ "wolf", "scavenger", "archer", "scavenger" }),
            (new(47, 58), 2, new[]{ "scavenger", "archer", "wolf", "spore" }),
            (new(22, 43), 2, new[]{ "wolf", "spore", "scavenger", "wolf" }),
            (new(47, 29), 2, new[]{ "archer", "scavenger", "spore", "wolf" }),
            (new(80, 44), 2, new[]{ "scavenger", "archer", "guardian", "wolf" }),
            (new(104, 48), 3, new[]{ "guardian", "archer", "scavenger", "wolf" }),
            (new(143, 42), 3, new[]{ "archer", "guardian", "scavenger", "archer" }),
            (new(174, 58), 3, new[]{ "guardian", "archer", "wolf", "scavenger" }),
            (new(99, 105), 2, new[]{ "scavenger", "wolf", "archer", "scavenger" }),
            (new(51, 151), 3, new[]{ "wolf", "spore", "scavenger", "spore" }),
            (new(86, 161), 3, new[]{ "guardian", "archer", "scavenger", "wolf" }),
            (new(131, 130), 4, new[]{ "spore", "wolf", "guardian", "spore" }),
            (new(167, 104), 4, new[]{ "spore", "archer", "wolf", "spore" }),
            (new(193, 122), 4, new[]{ "spore", "guardian", "archer", "wolf" }),
            (new(156, 158), 4, new[]{ "guardian", "archer", "wolf", "guardian" }),
            (new(193, 183), 5, new[]{ "wolf", "guardian", "archer", "guardian" }),
            (new(143, 182), 5, new[]{ "wolf", "archer", "guardian", "wolf" }),
            (new(197, 206), 6, new[]{ "guardian", "archer", "wolf", "guardian" })
        };
        Vector2I[] offsets = { new(-3, -2), new(3, -2), new(-2, 3), new(3, 3) };
        for (int g = 0; g < groups.Length; g++)
        for (int i = 0; i < groups[g].Kinds.Length; i++)
        {
            Vector2I tile = groups[g].Tile + offsets[i];
            ClearDisc(tile, 1, Tiles[tile.X, tile.Y] == Terrain.Water ? Terrain.Wetland : Tiles[tile.X, tile.Y]);
            ConnectToRoad(tile);
            EnemySpawns.Add(new($"world_{g}_{i}", groups[g].Kinds[i], tile, groups[g].Level));
        }
        foreach (var (id, tile, level) in new (string, Vector2I, int)[]
        {
            ("forest_elite", new(24, 58), 3),
            ("dunes_elite", new(184, 42), 4),
            ("wetland_elite", new(207, 112), 5),
            ("snow_elite", new(185, 202), 6)
        })
        {
            ClearDisc(tile, 2, Tiles[tile.X, tile.Y]);
            ConnectToRoad(tile);
            EnemySpawns.Add(new(id, "elite", tile, level));
        }
        ClearDisc(new(56, 87), 3, Terrain.Meadow);
        EnsureContentReachable();
    }

    private void ConnectToRoad(Vector2I tile)
    {
        Vector2I nearest = tile;
        float best = float.PositiveInfinity;
        for (int y = Math.Max(2, tile.Y - 24); y <= Math.Min(Size - 3, tile.Y + 24); y++)
        for (int x = Math.Max(2, tile.X - 24); x <= Math.Min(Size - 3, tile.X + 24); x++)
        {
            if (Tiles[x, y] != Terrain.Path || Blocked[x, y]) continue;
            float distance = tile.DistanceSquaredTo(new(x, y));
            if (distance < best) { best = distance; nearest = new(x, y); }
        }
        if (nearest != tile) Path(tile, nearest);
    }

    private void EnsureContentReachable()
    {
        var visited = new bool[Width, Height];
        var queue = new Queue<Vector2I>();
        Vector2I start = new(56, 87);
        visited[start.X, start.Y] = true;
        queue.Enqueue(start);
        Vector2I[] directions = { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down };
        void Flood()
        {
            while (queue.Count > 0)
            {
                Vector2I at = queue.Dequeue();
                foreach (Vector2I direction in directions)
                {
                    Vector2I next = at + direction;
                    if (IsBlocked(next.X, next.Y) || visited[next.X, next.Y]) continue;
                    visited[next.X, next.Y] = true;
                    queue.Enqueue(next);
                }
            }
        }
        Flood();
        var targets = new List<Vector2I>();
        foreach (WorldObject obj in Objects) targets.Add(obj.Tile);
        foreach (EnemySpawn spawn in EnemySpawns) targets.Add(spawn.Tile);
        foreach (Vector2I tile in targets)
        {
            if (visited[tile.X, tile.Y]) continue;
            Vector2I nearest = start;
            int best = int.MaxValue;
            for (int y = 2; y < Height - 2; y++)
            for (int x = 2; x < Width - 2; x++)
            {
                if (!visited[x, y]) continue;
                int distance = Math.Abs(x - tile.X) + Math.Abs(y - tile.Y);
                if (distance < best) { best = distance; nearest = new(x, y); }
            }
            Path(tile, nearest);
            visited[nearest.X, nearest.Y] = true;
            queue.Enqueue(nearest);
            Flood();
        }
    }

    private float Noise(float x, float y)
    {
        int ix = (int)x, iy = (int)y;
        float tx = x - ix, ty = y - iy;
        tx = tx * tx * (3 - 2 * tx);
        ty = ty * ty * (3 - 2 * ty);
        float Sample(int sx, int sy) => (Hash(sx, sy, Seed) % 10000) / 10000f;
        return Mathf.Lerp(Mathf.Lerp(Sample(ix, iy), Sample(ix + 1, iy), tx),
            Mathf.Lerp(Sample(ix, iy + 1), Sample(ix + 1, iy + 1), tx), ty);
    }

    internal static uint Hash(int x, int y, int seed)
    {
        uint value = unchecked((uint)(x * 374761393 + y * 668265263 + seed * 1442695));
        value = (value ^ (value >> 13)) * 1274126177;
        return value ^ (value >> 16);
    }
}
