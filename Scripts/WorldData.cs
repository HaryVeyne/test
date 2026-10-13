using Godot;
using System;
using System.Collections.Generic;

namespace ProjectUnknown;

public enum Terrain { Meadow, Forest, Dunes, Water, Ruins, Path }
public record Landmark(string Id, string Name, Vector2I Tile, float Radius = 5);

public sealed class WorldData
{
    public const int Size = 120;
    public const int TileSize = 32;
    public int Seed { get; }
    public Terrain[,] Tiles { get; } = new Terrain[Size, Size];
    public bool[,] Blocked { get; } = new bool[Size, Size];
    public List<Landmark> Landmarks { get; } = new()
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
    public Vector2 Spawn => Center(new(56, 87));

    public WorldData(int seed)
    {
        Seed = seed;
        GenerateTerrain();
        LayPaths();
        PlaceLandmarks();
        BuildLake();
    }

    public static Vector2 Center(Vector2I tile) => (Vector2)tile * TileSize + Vector2.One * (TileSize / 2);
    public Terrain TileAt(int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size ? Tiles[x, y] : Terrain.Water;
    public bool IsBlocked(int x, int y, bool bridgeOpen = false)
    {
        if (x < 0 || y < 0 || x >= Size || y >= Size) return true;
        if (bridgeOpen && x >= 26 && x <= 28 && y >= 81 && y <= 83) return false;
        return Blocked[x, y];
    }

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
            Tiles[x, y] = terrain;
            uint h = Hash(x, y, Seed);
            Blocked[x, y] = (terrain == Terrain.Forest && h % 100 < 23)
                || (terrain == Terrain.Dunes && h % 180 == 0);
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
            int radius = landmark.Id == "center" ? 7 : landmark.Id is "forest" or "dunes" ? 5 : 3;
            Terrain floor = landmark.Id is "center" or "archive" or "bell" ? Terrain.Ruins
                : landmark.Id == "dunes" ? Terrain.Dunes : Terrain.Meadow;
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
