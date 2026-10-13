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
    private static readonly HashSet<string> Mechanisms = new()
    {
        "bridge", "forest", "lake", "dunes", "center", "crypt_supply_a", "crypt_supply_b", "crypt_gate",
        "forge_coolant", "forge_gate", "crypt_complete", "forge_complete"
    };
    private static readonly HashSet<string> Areas = new() { "world", "crypt", "forge" };
    private static readonly HashSet<string> CheckpointIds = new() { "camp", "forest_camp", "dunes_camp", "wetland_camp", "snow_camp" };

    public static bool Exists => Godot.FileAccess.FileExists(SavePath);

    public static bool ArchiveForNewGame(out string error)
    {
        error = "";
        if (!Exists) return true;
        string previous = $"user://project_unknown_previous_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.json";
        Error moved = DirAccess.RenameAbsolute(SavePath, previous);
        if (moved != Error.Ok)
        {
            error = "无法备份旧存档：" + moved;
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
            error = "尚无存档，请开始新游戏。";
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
            bool legacy = state.Version == 1;
            if (legacy) MigrateLegacy(state);
            var world = new WorldData(state.Seed, state.Area);
            if (state.Activated.Contains("bridge") && state.Area == "world") world.OpenBridge();
            string gate = state.Area + "_gate";
            if (state.Area != "world" && state.Activated.Contains(gate)) world.OpenGate(gate);
            if (world.IsBlocked((int)(state.PlayerX / WorldData.TileSize), (int)((state.PlayerY + 3) / WorldData.TileSize), state.Activated.Contains("bridge")))
            {
                if (!legacy || !RelocateLegacyPlayer(state, world))
                {
                    error = "存档中的玩家位置位于不可通行地形。原文件已保留。";
                    return null;
                }
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
            Version = "0.2",
            ExportedUtc = DateTime.UtcNow,
            state.Seed,
            Result = state.RpgCompleted ? "completed" : state.Completed ? "relic_completed" : "in_progress",
            Stats = new
            {
                state.PlayedSeconds,
                state.Distance,
                state.MoveCount,
                state.InteractionCount,
                ExploredTiles = state.Explored.Count,
                ExploredPercent = Math.Round(state.Explored.Count * 100.0 / (WorldData.Size * WorldData.Size), 2),
                LandmarksFound = state.Discovered.Count,
                state.EnemyKills,
                state.BossKills,
                state.Deaths,
                state.DamageDealt,
                state.DamageTaken,
                ChestsOpened = state.OpenedChests.Count,
                state.Level,
                state.Experience,
                DungeonTilesExplored = state.DungeonExplored.ToDictionary(pair => pair.Key, pair => pair.Value.Count)
            },
            state.Regions,
            state.Discovered,
            state.Activated,
            state.BossesDefeated,
            state.Checkpoints,
            Equipment = new { state.WeaponId, state.ArmorId, state.AccessoryId },
            state.Inventory,
            state.LearnedSkills,
            state.Passives,
            state.Journal,
            state.Events
        };
        file.StoreString(JsonSerializer.Serialize(report, JsonOptions));
        file.Flush();
        return file.GetError() == Error.Ok ? ProjectSettings.GlobalizePath(path) : "导出失败：" + file.GetError();
    }

    private static string Validate(GameState state)
    {
        if (state.Version is not (1 or 2)) return "此存档版本不受 V0.2 支持。";
        bool legacy = state.Version == 1;
        int width = legacy ? 120 : state.Area == "world" ? WorldData.Size : 56;
        int height = legacy ? 120 : state.Area == "world" ? WorldData.Size : 44;
        if (!ValidPosition(state.PlayerX, state.PlayerY, width, height))
            return "存档中的玩家位置越过世界边界。";
        if (!double.IsFinite(state.PlayedSeconds) || state.PlayedSeconds < 0 || !float.IsFinite(state.Distance) || state.Distance < 0 || state.MoveCount < 0 || state.InteractionCount < 0)
            return "存档中的探索统计无效。";
        if (state.Explored == null || state.Activated == null || state.Discovered == null || state.Regions == null || state.Journal == null || state.Events == null || state.Mirrors == null)
            return "存档缺少探索数据。";
        int worldSize = legacy ? 120 : WorldData.Size;
        if (state.Explored.Count > worldSize * worldSize || state.Explored.Any(tile => tile < 0 || tile >= worldSize * worldSize)
            || state.Activated.Any(id => !Mechanisms.Contains(id)) || !ValidIds(state.Discovered, 200) || !ValidIds(state.Regions, 20))
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
        if (legacy) return "";
        if (state.Inventory == null || state.LearnedSkills == null || state.Passives == null || state.KilledEnemies == null
            || state.BossesDefeated == null || state.OpenedChests == null || state.Checkpoints == null
            || state.DungeonExplored == null || state.GroundLoot == null)
            return "存档缺少角色或物品数据。";
        if (state.Area == null || !Areas.Contains(state.Area) || !ValidPosition(state.WorldX, state.WorldY, WorldData.Size, WorldData.Size)
            || state.CheckpointId == null || !CheckpointIds.Contains(state.CheckpointId) || !state.Checkpoints.Contains(state.CheckpointId)
            || state.Checkpoints.Any(id => !CheckpointIds.Contains(id)))
            return "存档中的关卡或检查点无效。";
        if (state.Inventory.Count > 100 || state.Inventory.Any(item => item == null || item.Id == null || !Items.All.ContainsKey(item.Id) || item.Count < 1 || item.Count > 9999)
            || state.Inventory.Select(item => item.Id).Distinct().Count() != state.Inventory.Count)
            return "存档中的背包物品无效。";
        foreach (var slot in new[] { (state.WeaponId, ItemKind.Weapon), (state.ArmorId, ItemKind.Armor), (state.AccessoryId, ItemKind.Accessory) })
            if (slot.Item1 == null || slot.Item1.Length > 0 && (!Items.All.TryGetValue(slot.Item1, out var item) || item.Kind != slot.Item2 || !state.Inventory.Any(stack => stack.Id == slot.Item1)))
                return "存档中的装备无效。";
        if (state.Level < 1 || state.Level > 100 || state.Experience < 0 || state.Experience >= Progression.RequiredXp(state.Level)
            || state.SkillPoints < 0 || state.SkillPoints > state.Level + 1 + (state.LegacyRewardClaimed ? 2 : 0) || state.LearnedSkills.Any(id => !Progression.ActiveSkills.Contains(id))
            || state.Passives.Any(pair => !Progression.PassiveSkills.Contains(pair.Key) || pair.Value < 1 || pair.Value > 3))
            return "存档中的等级或技能无效。";
        if (!float.IsFinite(state.Health) || state.Health < 0 || state.Health > Progression.Stats(state).MaxHealth)
            return "存档中的生命值无效。";
        if (!ValidIds(state.KilledEnemies, 1000) || !ValidIds(state.OpenedChests, 200)
            || state.BossesDefeated.Any(id => id is not ("root_boss" or "forge_boss"))
            || state.EnemyKills < 0 || state.BossKills < 0 || state.BossKills > 2 || state.Deaths < 0 || state.DamageDealt < 0 || state.DamageTaken < 0
            || state.RpgCompleted && (!state.Completed || !state.BossesDefeated.Contains("root_boss") || !state.BossesDefeated.Contains("forge_boss")))
            return "存档中的战斗进度无效。";
        if (state.DungeonExplored.Any(pair => pair.Key is not ("crypt" or "forge") || pair.Value == null || pair.Value.Any(tile => tile < 0 || tile >= 56 * 44)))
            return "存档中的地下城地图无效。";
        if (state.GroundLoot.Count > 1000 || state.GroundLoot.Any(loot => loot == null || loot.Id == null || !Items.All.ContainsKey(loot.Id)
            || loot.Count < 1 || loot.Count > 9999 || loot.Area == null || !Areas.Contains(loot.Area)
            || !ValidPosition(loot.X, loot.Y, loot.Area == "world" ? WorldData.Size : 56, loot.Area == "world" ? WorldData.Size : 44)))
            return "存档中的地面物品无效。";
        return "";
    }

    private static bool ValidPosition(float x, float y, int width, int height) =>
        float.IsFinite(x) && float.IsFinite(y) && x >= 0 && y >= 0 && x < width * WorldData.TileSize && y < height * WorldData.TileSize;

    private static bool ValidIds(HashSet<string> ids, int maximum) => ids.Count <= maximum && ids.All(id => id != null && id.Length is > 0 and <= 100);

    private static void MigrateLegacy(GameState state)
    {
        state.Explored = state.Explored.Select(tile => tile / 120 * WorldData.Size + tile % 120).ToHashSet();
        state.Version = 2;
        state.Area = "world";
        state.WorldX = state.PlayerX;
        state.WorldY = state.PlayerY;
        var defaults = new GameState();
        state.Health = defaults.Health;
        state.Level = defaults.Level;
        state.Experience = defaults.Experience;
        state.SkillPoints = defaults.SkillPoints;
        state.Inventory = defaults.Inventory;
        state.WeaponId = defaults.WeaponId;
        state.ArmorId = defaults.ArmorId;
        state.AccessoryId = defaults.AccessoryId;
        state.LearnedSkills = defaults.LearnedSkills;
        state.Passives = defaults.Passives;
        state.KilledEnemies = defaults.KilledEnemies;
        state.BossesDefeated = defaults.BossesDefeated;
        state.OpenedChests = defaults.OpenedChests;
        state.Checkpoints = defaults.Checkpoints;
        state.CheckpointId = defaults.CheckpointId;
        state.DungeonExplored = defaults.DungeonExplored;
        state.GroundLoot = defaults.GroundLoot;
        state.EnemyKills = state.Deaths = state.BossKills = state.DamageDealt = state.DamageTaken = 0;
        state.RpgCompleted = false;
        state.LegacyRewardClaimed = false;
        state.Events.Add(new ExplorationEvent(state.PlayedSeconds, "save_migrated", "V0.1 → V0.2"));
    }

    private static bool RelocateLegacyPlayer(GameState state, WorldData world)
    {
        var origin = new Vector2I((int)(state.PlayerX / WorldData.TileSize), (int)(state.PlayerY / WorldData.TileSize));
        for (int radius = 1; radius <= 8; radius++)
        for (int y = origin.Y - radius; y <= origin.Y + radius; y++)
        for (int x = origin.X - radius; x <= origin.X + radius; x++)
        {
            if (Math.Max(Math.Abs(x - origin.X), Math.Abs(y - origin.Y)) != radius || world.IsBlocked(x, y)) continue;
            var position = WorldData.Center(new Vector2I(x, y));
            state.PlayerX = state.WorldX = position.X;
            state.PlayerY = state.WorldY = position.Y;
            state.Events.Add(new ExplorationEvent(state.PlayedSeconds, "save_relocated", "旧存档位置已调整至邻近可通行地形"));
            return true;
        }
        return false;
    }
}
