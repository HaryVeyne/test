using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ProjectUnknown;

public static class SaveStore
{
    private const string SavePath = "user://project_unknown_save.json";
    private const long MaxFileBytes = 8 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly HashSet<string> Mechanisms = new() { "bridge", "forest", "lake", "dunes", "center" };
    private static readonly HashSet<string> Landmarks = new() { "camp", "center", "forest", "dunes", "lake", "overlook", "archive", "hollow", "bell" };

    public static bool Exists => Godot.FileAccess.FileExists(SavePath);

    public static bool ArchiveForNewGame(out string error)
    {
        error = "";
        if (!Exists) return true;
        string previous = $"user://project_unknown_previous_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.json";
        Error moved = DirAccess.RenameAbsolute(SavePath, previous);
        if (moved != Error.Ok)
        {
            error = "无法保留上一段旅程的存档：" + moved;
            return false;
        }
        return true;
    }

    public static bool Save(GameState state, out string error)
    {
        error = Validate(state);
        if (error.Length > 0) return false;
        if (Exists && Load(out string existingError) == null)
        {
            error = "未覆盖损坏的存档。" + existingError;
            return false;
        }

        string json = JsonSerializer.Serialize(state, JsonOptions);
        string temporary = SavePath + ".tmp";
        using (var file = Godot.FileAccess.Open(temporary, Godot.FileAccess.ModeFlags.Write))
        {
            if (file == null)
            {
                error = "无法写入存档：" + Godot.FileAccess.GetOpenError();
                return false;
            }
            file.StoreString(json);
            file.Flush();
            if (file.GetError() != Error.Ok)
            {
                error = "写入存档失败：" + file.GetError();
                return false;
            }
        }

        string backup = SavePath + ".bak";
        bool hadPrevious = Exists;
        if (hadPrevious)
        {
            if (Godot.FileAccess.FileExists(backup)) DirAccess.RemoveAbsolute(backup);
            Error movePrevious = DirAccess.RenameAbsolute(SavePath, backup);
            if (movePrevious != Error.Ok)
            {
                error = "无法替换旧存档：" + movePrevious;
                return false;
            }
        }
        Error replace = DirAccess.RenameAbsolute(temporary, SavePath);
        if (replace != Error.Ok)
        {
            if (hadPrevious) DirAccess.RenameAbsolute(backup, SavePath);
            error = "保存未完成，旧存档已保留：" + replace;
            return false;
        }
        if (hadPrevious) DirAccess.RemoveAbsolute(backup);
        return true;
    }

    public static GameState? Load(out string error)
    {
        error = "";
        if (!Exists)
        {
            error = "尚无存档，可以开始新的旅程。";
            return null;
        }
        using var file = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Read);
        if (file == null)
        {
            error = "无法读取存档：" + Godot.FileAccess.GetOpenError();
            return null;
        }
        if (file.GetLength() > MaxFileBytes)
        {
            error = "存档文件过大，无法安全读取。";
            return null;
        }

        try
        {
            string json = file.GetAsText();
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("Version", out _))
            {
                error = "存档缺少版本信息。";
                return null;
            }
            var state = JsonSerializer.Deserialize<GameState>(json, JsonOptions);
            if (state == null)
            {
                error = "存档内容为空。";
                return null;
            }
            error = Validate(state);
            if (error.Length > 0) return null;
            var world = new WorldData(state.Seed);
            if (world.IsBlocked((int)(state.PlayerX / WorldData.TileSize), (int)((state.PlayerY + 3) / WorldData.TileSize), state.Activated.Contains("bridge")))
            {
                error = "存档中的玩家位置位于不可通行地形。原文件已保留。";
                return null;
            }
            return state;
        }
        catch (JsonException)
        {
            error = "存档 JSON 已损坏。原文件已保留。";
            return null;
        }
    }

    public static string Export(GameState state)
    {
        string path = $"user://exploration_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.json";
        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Write);
        if (file == null) return "导出失败：" + Godot.FileAccess.GetOpenError();
        var report = new
        {
            Project = "Project Unknown",
            Version = "0.1",
            ExportedUtc = DateTime.UtcNow,
            state.Seed,
            Result = state.Completed ? "completed" : "in_progress",
            Stats = new
            {
                state.PlayedSeconds,
                state.Distance,
                state.MoveCount,
                state.InteractionCount,
                ExploredTiles = state.Explored.Count,
                ExploredPercent = Math.Round(state.Explored.Count * 100.0 / (WorldData.Size * WorldData.Size), 2),
                LandmarksFound = state.Discovered.Count,
                TileDistance = Math.Round(state.Distance / WorldData.TileSize, 2),
                UniqueTilesPerTileMoved = state.Distance > 0 ? Math.Round(state.Explored.Count / (state.Distance / WorldData.TileSize), 3) : 0
            },
            state.Regions,
            state.Discovered,
            state.Activated,
            state.Journal,
            state.Events
        };
        file.StoreString(JsonSerializer.Serialize(report, JsonOptions));
        file.Flush();
        return file.GetError() == Error.Ok ? ProjectSettings.GlobalizePath(path) : "导出失败：" + file.GetError();
    }

    private static string Validate(GameState state)
    {
        if (state.Version != 1) return "此存档版本不受 V0.1 支持。";
        float extent = WorldData.Size * WorldData.TileSize;
        if (!float.IsFinite(state.PlayerX) || !float.IsFinite(state.PlayerY) || state.PlayerX < 0 || state.PlayerY < 0 || state.PlayerX >= extent || state.PlayerY >= extent)
            return "存档中的玩家位置越过世界边界。";
        if (!double.IsFinite(state.PlayedSeconds) || state.PlayedSeconds < 0 || !float.IsFinite(state.Distance) || state.Distance < 0 || state.MoveCount < 0 || state.InteractionCount < 0)
            return "存档中的探索统计无效。";
        if (state.Explored == null || state.Activated == null || state.Discovered == null || state.Regions == null || state.Journal == null || state.Events == null || state.Mirrors == null)
            return "存档缺少探索数据。";
        if (state.Explored.Count > WorldData.Size * WorldData.Size || state.Explored.Any(tile => tile < 0 || tile >= WorldData.Size * WorldData.Size)
            || state.Activated.Any(id => !Mechanisms.Contains(id)) || state.Discovered.Any(id => !Landmarks.Contains(id)) || state.Regions.Count > 12)
            return "存档中的地图或机关数据无效。";
        if (state.RuneStep < 0 || state.RuneStep > 3 || state.Mirrors.Length != 2 || state.Mirrors.Any(direction => direction < 0 || direction > 3))
            return "存档中的机关状态无效。";
        if (state.Journal.Count > 1000 || state.Events.Count > 30000 || state.Journal.Any(entry => entry == null || !double.IsFinite(entry.Time) || entry.Time < 0 || entry.Text == null || entry.Text.Length > 4000)
            || state.Events.Any(entry => entry == null || !double.IsFinite(entry.Time) || entry.Time < 0 || entry.Action == null || entry.Action.Length > 100 || entry.Detail == null || entry.Detail.Length > 4000))
            return "存档中的探索记录无效。";
        bool connected = state.Activated.Contains("forest") && state.Activated.Contains("lake") && state.Activated.Contains("dunes");
        if (state.Activated.Contains("forest") != (state.RuneStep == 3) || state.Activated.Contains("lake") && !state.Activated.Contains("bridge")
            || state.Activated.Contains("dunes") != (state.Mirrors[0] == 0 && state.Mirrors[1] == 2)
            || state.Activated.Contains("center") != connected || state.Completed && !connected)
            return "存档中的关键进度不一致。";
        return "";
    }
}
