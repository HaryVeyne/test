using System.Collections.Generic;

namespace ProjectUnknown;

public sealed class GameState
{
    public int Version { get; set; } = 2;
    public int Seed { get; set; } = 73129;
    public float PlayerX { get; set; }
    public float PlayerY { get; set; }
    public double PlayedSeconds { get; set; }
    public float Distance { get; set; }
    public int MoveCount { get; set; }
    public int InteractionCount { get; set; }
    public bool Completed { get; set; }
    public HashSet<int> Explored { get; set; } = new();
    public HashSet<string> Activated { get; set; } = new();
    public HashSet<string> Discovered { get; set; } = new();
    public HashSet<string> Regions { get; set; } = new();
    public List<JournalEntry> Journal { get; set; } = new();
    public List<ExplorationEvent> Events { get; set; } = new();
    public int RuneStep { get; set; }
    public int[] Mirrors { get; set; } = new[] { 1, 3 };
    public string Area { get; set; } = "world";
    public float WorldX { get; set; }
    public float WorldY { get; set; }
    public float Health { get; set; } = 100;
    public int Level { get; set; } = 1;
    public int Experience { get; set; }
    public int SkillPoints { get; set; } = 2;
    public List<ItemStack> Inventory { get; set; } = new()
    {
        new("rust_sword", 1), new("hunter_bow", 1), new("ember_staff", 1),
        new("linen_armor", 1), new("health_potion", 3)
    };
    public string WeaponId { get; set; } = "rust_sword";
    public string ArmorId { get; set; } = "linen_armor";
    public string AccessoryId { get; set; } = "";
    public HashSet<string> LearnedSkills { get; set; } = new() { "cleave" };
    public Dictionary<string, int> Passives { get; set; } = new();
    public HashSet<string> KilledEnemies { get; set; } = new();
    public HashSet<string> BossesDefeated { get; set; } = new();
    public HashSet<string> OpenedChests { get; set; } = new();
    public HashSet<string> Checkpoints { get; set; } = new() { "camp" };
    public string CheckpointId { get; set; } = "camp";
    public Dictionary<string, HashSet<int>> DungeonExplored { get; set; } = new();
    public List<LootState> GroundLoot { get; set; } = new();
    public int EnemyKills { get; set; }
    public int Deaths { get; set; }
    public int BossKills { get; set; }
    public int DamageDealt { get; set; }
    public int DamageTaken { get; set; }
    public bool RpgCompleted { get; set; }
    public bool LegacyRewardClaimed { get; set; }

    public HashSet<int> ExploredFor(string area)
    {
        if (area == "world") return Explored;
        if (!DungeonExplored.TryGetValue(area, out var explored))
            DungeonExplored[area] = explored = new HashSet<int>();
        return explored;
    }
}

public sealed class ItemStack
{
    public string Id { get; set; }
    public int Count { get; set; }
    public ItemStack(string id, int count) { Id = id; Count = count; }
}

public sealed class LootState
{
    public string Id { get; set; } = "";
    public int Count { get; set; } = 1;
    public string Area { get; set; } = "world";
    public float X { get; set; }
    public float Y { get; set; }
}

public sealed record JournalEntry(double Time, string Text);

public sealed record ExplorationEvent(double Time, string Action, string Detail);
