using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace ProjectUnknown;

// Integration checks run inside the real main scene; positioning skips travel, not combat rules.
public partial class RuntimeVerification : Node
{
    public Game Game { get; init; } = null!;
    private const string SavePath = "user://project_unknown_save.json";
    private int _checks;
    private Vector2 _combatSpace;

    public override async void _Ready()
    {
        try
        {
            GD.Print("VERIFICATION: real physics and combat; test positioning skips travel; routes checked separately by flood fill.");
            Game.StartNew();
            Game.Pause();
            VerifyWorld();
            await VerifyMovementAndDamage();
            await VerifyOriginalPuzzles();
            await VerifyCombatAndEquipment();
            await VerifyProgressionAndSkills();
            await VerifyDeathAndUi();
            await VerifyDungeon("crypt", "root_boss", "guardian_blade");
            await VerifyDungeon("forge", "forge_boss", "solar_staff");
            await VerifySavingAndMigration();
            GD.Print($"VERIFICATION PASSED: {_checks} checks; both bosses defeated by real attacks and skills.");
            Game.QuitGame();
        }
        catch (Exception exception)
        {
            ReleaseMovement();
            GD.PushError("VERIFICATION FAILED: " + exception);
            GetTree().Quit(1);
        }
    }

    private void VerifyWorld()
    {
        var world = Game.World;
        var same = new WorldData(Game.State.Seed);
        Check(world.Width * world.Height >= 120 * 120 * 3, "surface area at least three times V0.1");
        Check(Enumerable.Range(0, world.Width * world.Height).All(i =>
            world.Tiles[i % world.Width, i / world.Width] == same.Tiles[i % world.Width, i / world.Width]
            && world.Blocked[i % world.Width, i / world.Width] == same.Blocked[i % world.Width, i / world.Width]), "fixed seed terrain and collision determinism");
        Check(world.Objects.SequenceEqual(same.Objects) && world.EnemySpawns.SequenceEqual(same.EnemySpawns), "fixed seed content determinism");
        var terrain = world.Tiles.Cast<Terrain>().ToHashSet();
        Check(new[] { Terrain.Forest, Terrain.Meadow, Terrain.Dunes, Terrain.Wetland, Terrain.Snow }.All(terrain.Contains), "five distinct surface regions");
        var reached = Reachable(world, new(56, 87));
        Check(world.Objects.All(obj => reached.Contains(obj.Tile)), "all checkpoints, portals and chests reachable");
        Check(world.EnemySpawns.All(enemy => reached.Contains(enemy.Tile)), "all surface encounters reachable");
        foreach (var tile in new Vector2I[] { new(60, 59), new(24, 32), new(27, 29), new(30, 32), new(91, 43), new(97, 43), new(23, 82) })
            Check(reached.Contains(tile), "original puzzle reachable " + tile);
        Check(!reached.Contains(new(31, 82)), "tide island retains its initial gate");
        Check(new[] { "scavenger", "archer", "wolf", "guardian", "spore", "elite" }.All(kind => world.EnemySpawns.Any(enemy => enemy.Kind == kind)), "five ordinary enemy types and elite have actual spawns");
        Check(Game.Combat.Enemies.Count == world.EnemySpawns.Count, "world encounter nodes instantiated");
        foreach (string area in new[] { "crypt", "forge" })
        {
            var dungeon = new WorldData(Game.State.Seed, area);
            reached = Reachable(dungeon, new(6, 22));
            Check(dungeon.Width == 56 && dungeon.Height == 44 && dungeon.Landmarks.Count >= 5 && dungeon.EnemySpawns.Count >= 10,
                area + " has rooms, encounters and a boss");
            Check(dungeon.Objects.Where(obj => obj.Kind == "switch").All(obj => reached.Contains(obj.Tile)) && !reached.Contains(new(46, 22)),
                area + " switches reachable while boss gate closed");
            dungeon.OpenGate(area + "_gate");
            reached = Reachable(dungeon, new(6, 22));
            Check(dungeon.Objects.All(obj => reached.Contains(obj.Tile)) && dungeon.EnemySpawns.All(enemy => reached.Contains(enemy.Tile)),
                area + " all rooms, loot and exits reachable after gate opens");
        }
    }

    private async Task VerifyMovementAndDamage()
    {
        Game.Resume();
        Position(new(60, 70));
        Vector2 before = Game.Wanderer.Position;
        Input.ActionPress("move_right");
        await Frames(40);
        Input.ActionRelease("move_right");
        await Frames(8);
        float walked = before.DistanceTo(Game.Wanderer.Position);
        Check(walked > 60 && Game.State.Distance > 60 && Game.State.MoveCount > 0, "native movement records useful travel statistics");
        Position(new(60, 70));
        before = Game.Wanderer.Position;
        Input.ActionPress("move_right"); Input.ActionPress("run");
        await Frames(40);
        Input.ActionRelease("move_right"); Input.ActionRelease("run");
        await Frames(8);
        Check(before.DistanceTo(Game.Wanderer.Position) > walked * 1.2f, "Shift increases movement speed");
        Position(new(56, 87));
        before = Game.Wanderer.Position;
        float health = Game.State.Health;
        Input.ActionPress("move_right");
        Check(Game.Wanderer.TryDodge() && Game.Wanderer.Invulnerable && Game.Wanderer.DodgeCooldown > 0, "dodge begins with invulnerability and cooldown");
        Check(!Game.Combat.DamagePlayer(30, before - Vector2.Right * 20) && Game.State.Health == health, "dodge prevents actual damage");
        await Frames(12);
        Input.ActionRelease("move_right");
        Check(before.DistanceTo(Game.Wanderer.Position) > 70 && !Game.Wanderer.TryDodge(), "dodge displaces player and prevents immediate repeat");
        await Frames(35);
        Position(new(56, 87));
        int expected = Math.Max(1, 25 - Game.Stats.Defense);
        before = Game.Wanderer.Position;
        Check(Game.Combat.DamagePlayer(25, before - Vector2.Right * 20) && Game.State.Health == health - expected, "defense reduces actual player damage");
        Check(Game.Wanderer.Invulnerable && !Game.Combat.DamagePlayer(25, before), "hit grants brief invulnerability");
        await Frames(15);
        Check(Game.Wanderer.Position.DistanceTo(before) > 5 && Game.State.DamageTaken >= expected, "hit knockback and damage statistics are real");
        Game.State.Health = Game.Stats.MaxHealth;
    }

    private async Task VerifyOriginalPuzzles()
    {
        Position(new(25, 82));
        Input.ActionPress("move_right");
        await Frames(50);
        Input.ActionRelease("move_right");
        await Frames(4);
        Check(Game.Wanderer.Position.X < 26 * 32, "unopened bridge blocks native collision");
        Game.Pause();
        Game.Puzzles.UpdatePlate(WorldData.Center(new(23, 82)), 0.5);
        Check(Game.Puzzles.PlateCharge > 0, "pressure plate charges while occupied");
        Game.Puzzles.UpdatePlate(WorldData.Center(new(23, 85)), 2);
        Check(Game.Puzzles.PlateCharge == 0, "pressure plate resets on leaving");
        Position(new(23, 82));
        Game.Resume();
        await Frames(165);
        Check(Game.State.Activated.Contains("bridge") && Reachable(Game.World, new(56, 87)).Contains(new(31, 82)), "scene dwelling opens bridge and makes island reachable");
        Position(new(25, 82));
        Input.ActionPress("move_right");
        await Frames(95);
        Input.ActionRelease("move_right");
        await Frames(5);
        Check(Game.Wanderer.Position.X > 29 * 32, "bridge opening updates native collision");
        InteractAt(new(31, 82));
        Check(Game.State.Activated.Contains("lake"), "lake beacon activates");
        InteractAt(new(24, 32));
        Check(Game.State.RuneStep == 0, "incorrect rune resets sequence");
        InteractAt(new(27, 29));
        Check(Game.State.RuneStep == 1, "first rune records partial progress");
        Game.Pause();
        Game.SaveGame();
        var restored = SaveStore.Load(out string error);
        Check(restored != null && restored.Version == 2 && restored.RuneStep == 1 && restored.Activated.Contains("lake")
            && restored.Explored.Count > 0 && Math.Abs(restored.PlayerX - Game.Wanderer.Position.X) < 0.1f, "partial puzzle, position and fog load: " + error);
        Game.ContinueGame();
        Check(Game.IsPlaying && Game.State.RuneStep == 1 && !Game.World.Blocked[26, 82], "continue rebuilds opened bridge and partial rune");
        InteractAt(new(24, 32)); InteractAt(new(30, 32));
        Check(Game.State.Activated.Contains("forest"), "ordered rune sequence completes");
        InteractAt(new(60, 59));
        Check(!Game.State.Completed, "central relic still needs all three regional discoveries");
        for (int i = 0; i < 3; i++) InteractAt(new(91, 43));
        for (int i = 0; i < 3; i++) InteractAt(new(97, 43));
        Check(Game.State.Activated.Contains("dunes") && Game.State.Activated.Contains("center"), "mirror mechanism still unlocks central relic");
        InteractAt(new(60, 59));
        Check(Game.State.Completed && Game.State.LegacyRewardClaimed && !Game.IsPlaying && Count("tide_charm") == 1, "relic awards actual equipment and shows result");
        Game.ResumeAfterResult();
        Check(Game.IsPlaying && !Game.State.RpgCompleted, "relic completion allows RPG adventure to continue");
        Game.SaveGame();
        Check(SaveStore.Load(out error)?.LegacyRewardClaimed == true, "relic reward skill points remain valid in saved data: " + error);
    }

    private async Task VerifyCombatAndEquipment()
    {
        var foe = Game.Combat.Enemies.First(enemy => enemy.Kind == "scavenger" && enemy.AiState is "Idle" or "Patrol");
        _combatSpace = FindCombatSpace(new HashSet<Enemy> { foe });
        Stage(foe, 145);
        string beforeAi = foe.AiState;
        await Frames(3);
        Check(beforeAi is "Idle" or "Patrol" && foe.AiState == "Alert", "ordinary enemy detects player and visibly alerts");
        float initialDistance = foe.Position.DistanceTo(Game.Wanderer.Position);
        await Frames(40);
        Check(foe.AiState == "Chase" && foe.Position.DistanceTo(Game.Wanderer.Position) < initialDistance - 10, "enemy chases through actual navigation");
        bool warning = false;
        float beforeHp = Game.State.Health;
        for (int i = 0; i < 140; i++)
        {
            if (foe.AiState == "Windup") { warning = true; Check(Game.State.Health == beforeHp, "melee windup precedes first damage"); break; }
            await Frames(1);
        }
        Check(warning, "enemy attack has a real windup state");
        PositionWorld(_combatSpace);
        foe.Position = _combatSpace + Vector2.Right * 52;
        Game.EquipItem("rust_sword");
        await WaitForAttack();
        int health = foe.Health;
        Check(Game.Combat.TryAttack(Vector2.Right), "sword attack starts");
        await Frames(3);
        Check(foe.Health == health && Game.Wanderer.AttackMotion > 0, "sword has visible startup before hit");
        await Frames(10);
        Check(foe.Health < health && Game.Combat.AttackCooldown > 0, "sword cone hits during active frames and has recovery");
        int kills = Game.State.EnemyKills;
        int experience = Game.State.Experience;
        await FightNormal(foe);
        Check(Game.State.EnemyKills == kills + 1 && Game.State.KilledEnemies.Contains(foe.SpawnId)
            && (Game.State.Experience > experience || Game.State.Level > 2), "normal combat death grants experience and persistent defeat");
        var drop = Game.State.GroundLoot.FirstOrDefault(loot => loot.Area == "world" && loot.Id == "health_potion");
        Check(drop != null, "normal enemy drops useful loot");
        int potions = Count("health_potion");
        PositionWorld(new(drop!.X, drop.Y));
        Game.InteractNearby();
        Check(Count("health_potion") == potions + drop.Count && !Game.State.GroundLoot.Contains(drop), "E picks up actual ground loot");

        OpenChest("old_watch_chest");
        int attack = Game.Stats.Attack;
        Game.EquipItem("copper_ring");
        Check(Game.State.AccessoryId == "copper_ring" && Game.Stats.Attack == attack + 3, "accessory changes computed attack");
        Game.UnequipItem("accessory");
        Check(Game.State.AccessoryId == "" && Game.Stats.Attack == attack, "unequip removes actual attribute bonus");
        Game.DropItem("copper_ring");
        Check(Count("copper_ring") == 0 && Game.State.GroundLoot.Any(loot => loot.Id == "copper_ring"), "discard creates a real saved ground item");
        Game.InteractNearby();
        Check(Count("copper_ring") == 1, "discarded equipment can be picked up again");
        OpenChest("caravan_chest");
        int defense = Game.Stats.Defense;
        Game.EquipItem("scale_armor");
        Check(Game.State.ArmorId == "scale_armor" && Game.Stats.Defense == defense + 4, "armor slot changes defense");
        OpenChest("dunes_cache");
        Game.EquipItem("iron_sword");
        Check(Game.Stats.Attack == attack + 6, "better sword changes combat attack");
        PositionWorld(_combatSpace);
        Game.State.Health = Game.Stats.MaxHealth - 75;
        potions = Count("health_potion");
        Game.UseItem("health_potion");
        Check(Game.State.Health == Game.Stats.MaxHealth - 15 && Count("health_potion") == potions - 1, "potion consumes one item and restores exactly 60 HP");

        var bowTarget = Game.Combat.Enemies.First(enemy => enemy.Kind == "guardian");
        Stage(bowTarget, 125);
        Game.EquipItem("hunter_bow");
        await WaitForAttack();
        health = bowTarget.Health;
        Check(Game.Combat.TryAttack(Vector2.Right), "bow attack starts");
        await Frames(10);
        var arrow = Game.Combat.GetChildren().OfType<Projectile>().FirstOrDefault(projectile => !projectile.EnemyProjectile && !projectile.Magic);
        Check(arrow != null && arrow.Speed > 500 && bowTarget.Health == health, "bow emits an aimed moving arrow after windup");
        await Frames(20);
        Check(bowTarget.Health < health, "actual arrow travel damages enemy");

        var magicTarget = Game.Combat.Enemies.First(enemy => enemy.Kind == "guardian" && enemy != bowTarget);
        PositionWorld(_combatSpace);
        magicTarget.Position = _combatSpace + Vector2.Right * 130;
        bowTarget.Position = _combatSpace + new Vector2(130, 28);
        Game.EquipItem("ember_staff");
        await WaitForAttack();
        health = magicTarget.Health;
        int secondaryHealth = bowTarget.Health;
        Check(Game.Combat.TryAttack(Vector2.Right), "staff attack starts");
        await Frames(16);
        var magic = Game.Combat.GetChildren().OfType<Projectile>().FirstOrDefault(projectile => !projectile.EnemyProjectile && projectile.Magic);
        Check(magic != null && magic.Speed < 400, "staff emits slower actual magic projectile");
        await Frames(25);
        Check(magicTarget.Health < health && bowTarget.Health < secondaryHealth, "staff impact explodes and damages nearby second enemy");
        PositionWorld(_combatSpace);
        Game.State.Health = Game.Stats.MaxHealth;
    }

    private async Task VerifyProgressionAndSkills()
    {
        float maxHealth = Game.Stats.MaxHealth;
        int attack = Game.Stats.Attack;
        int points = Game.State.SkillPoints;
        int level = Game.State.Level;
        int gained = Progression.GrantExperience(Game.State, Progression.RequiredXp(level));
        Check(gained == 1 && Game.State.Level == level + 1 && Game.State.SkillPoints == points + 1
            && Game.Stats.MaxHealth == maxHealth + 14 && Game.Stats.Attack == attack + 2, "level boundary raises HP, attack and skill points");
        // Grant the experience of a short encounter group so all six upgrades can be exercised.
        Progression.GrantExperience(Game.State, Progression.RequiredXp(Game.State.Level));
        Game.LearnSkill("volley"); Game.LearnSkill("nova");
        Check(Game.State.LearnedSkills.SetEquals(new[] { "cleave", "volley", "nova" }), "three active skills are genuinely learned");
        maxHealth = Game.Stats.MaxHealth; attack = Game.Stats.Attack;
        int defense = Game.Stats.Defense;
        points = Game.State.SkillPoints;
        Game.LearnSkill("vitality"); Game.LearnSkill("power"); Game.LearnSkill("guard");
        Check(Game.State.Passives.Count == 3 && Game.State.SkillPoints == points - 3 && Game.Stats.MaxHealth == maxHealth + 20
            && Game.Stats.Attack == attack + 3 && Game.Stats.Defense == defense + 2, "three passive choices spend points and change real stats");
        Game.EquipItem("iron_sword");
        var target = Game.Combat.Enemies.First(enemy => enemy.Kind == "elite");
        Stage(target, 72);
        int health = target.Health;
        Check(Game.Combat.TrySkill(0, target.Position) && target.Health < health && Game.Combat.SkillCooldowns[0] > 0, "cleave damages enemy and starts cooldown");
        Check(!Game.Combat.TrySkill(0, target.Position), "skill cooldown rejects repeated use");
        target.Position = _combatSpace + Vector2.Right * 130;
        PositionWorld(_combatSpace);
        health = target.Health;
        Check(Game.Combat.TrySkill(1, target.Position), "volley skill starts");
        Check(Game.Combat.GetChildren().OfType<Projectile>().Count(projectile => projectile.Piercing && !projectile.EnemyProjectile) == 3
            && Game.Combat.SkillCooldowns[1] > 0, "volley creates three actual piercing arrows and cooldown");
        await Frames(22);
        Check(target.Health < health, "volley projectiles cause damage");
        var novaTarget = Game.Combat.Enemies.First(enemy => enemy.Kind == "elite" && enemy != target);
        Stage(novaTarget, 160);
        health = novaTarget.Health;
        Check(Game.Combat.TrySkill(2, novaTarget.Position) && novaTarget.Health < health && novaTarget.SlowTime == 3
            && Game.Combat.SkillCooldowns[2] > 0, "nova deals aimed area damage and applies real slow");
        float cooldown = Game.Combat.SkillCooldowns[2];
        await Frames(15);
        Check(Game.Combat.SkillCooldowns[2] < cooldown && novaTarget.SlowTime < 3, "skill cooldown and slow expire during actual physics");
    }

    private async Task VerifyDeathAndUi()
    {
        Position(new(56, 87));
        Game.State.Health = Game.Stats.MaxHealth;
        await Frames(40);
        int deaths = Game.State.Deaths;
        Check(Game.Combat.DamagePlayer(10000, Game.Wanderer.Position - Vector2.Right * 20), "lethal actual damage applies");
        await Frames(3);
        Check(Game.State.Health == 0 && Game.Wanderer.IsDead && !Game.IsPlaying && Game.State.Deaths == deaths + 1, "death pauses play once and increments deaths");
        Game.Respawn();
        await Frames(3);
        var checkpoint = Game.World.Objects.Single(obj => obj.Id == Game.State.CheckpointId);
        Check(Game.IsPlaying && Game.State.Area == "world" && Game.State.Health == Game.Stats.MaxHealth
            && Game.Wanderer.Position.DistanceTo(WorldData.Center(checkpoint.Tile) + Vector2.Down * 40) < 2, "respawn returns to checkpoint with full HP and persistent progress");
        foreach (string action in new[] { "inventory", "character", "map", "journal" })
        {
            Game._UnhandledInput(new InputEventAction { Action = action, Pressed = true });
            await Frames(2);
            Check(!Game.IsPlaying && Game.GetChildren().OfType<GameUi>().Single().HasOverlay, action + " overlay opens and pauses combat");
            Game._UnhandledInput(new InputEventAction { Action = "pause", Pressed = true });
            Check(Game.IsPlaying, action + " overlay closes with Esc and resumes");
        }
        Game.TravelTo("camp");
        Check(Game.Wanderer.Position.DistanceTo(WorldData.Center(checkpoint.Tile) + Vector2.Down * 40) < 2, "activated checkpoint supports fast travel");
    }

    private async Task VerifyDungeon(string area, string bossKind, string reward)
    {
        var portal = Game.World.Objects.Single(obj => obj.Kind == "portal" && obj.Value == area);
        Vector2 entryPosition = WorldData.Center(portal.Tile);
        PositionWorld(entryPosition);
        Game.InteractNearby();
        await Frames(3);
        Check(Game.State.Area == area && Game.World.Area == area && Game.Dungeons != null && Game.IsPlaying, area + " portal switches to independent native area");
        var boss = Game.Combat.Enemies.Single(enemy => enemy.Kind == bossKind);
        int health = boss.Health;
        Check(!Game.Dungeons!.BossAccessible, area + " gate initially blocks boss access");
        // The skill invokes normal damage rules, proving the unopened arena cannot be bypassed.
        Vector2 previous = Game.Wanderer.Position;
        PositionWorld(boss.Position - Vector2.Right * 55);
        Game.Combat.TryAttack(Vector2.Right);
        await Frames(15);
        Check(boss.Health == health, area + " inaccessible boss rejects damage");
        PositionWorld(previous);
        if (area == "crypt")
        {
            InteractObject("crypt_supply_a");
            Check(Game.State.Activated.Contains("crypt_supply_a") && Game.World.Blocked[38, 22], "crypt first supply leaves gate locked");
            Game.Pause(); Game.SaveGame();
            var partial = SaveStore.Load(out string error);
            Check(partial?.Activated.Contains("crypt_supply_a") == true && !partial.Activated.Contains("crypt_gate"), "partial dungeon mechanism saves: " + error);
            Game.ContinueGame();
            boss = Game.Combat.Enemies.Single(enemy => enemy.Kind == bossKind);
            InteractObject("crypt_supply_b");
            Check(Game.State.Activated.Contains("crypt_gate") && !Game.World.Blocked[38, 22], "crypt dual supply opens physical gate");
            InteractObject("crypt_chest_south");
            Game.EquipItem("scale_armor");
        }
        else
        {
            Check(DungeonController.HeatWarning(3.2) && !DungeonController.HeatActive(3.2) && DungeonController.HeatActive(3.9), "forge floor warning precedes its damage phase");
            InteractObject("forge_coolant");
            Check(Game.State.Activated.Contains("forge_coolant") && Game.State.Activated.Contains("forge_gate") && !Game.World.Blocked[38, 22], "forge coolant disables hazard and opens gate");
            InteractObject("forge_chest_cargo");
            Game.EquipItem("frost_armor");
        }
        Check(Game.Dungeons!.BossAccessible && Reachable(Game.World, new(6, 22)).Contains(new(46, 22)), area + " boss arena is reachable after mechanism");
        Game.EquipItem(area == "crypt" ? "iron_sword" : "guardian_blade");
        Game.EquipItem("tide_charm");
        Game.State.Health = Game.Stats.MaxHealth;
        await FightBoss(boss);
        Check(Game.State.BossesDefeated.Contains(bossKind) && Game.State.Activated.Contains(area + "_complete")
            && Game.State.GroundLoot.Any(loot => loot.Id == reward && loot.Area == area), area + " real boss death completes dungeon and drops special equipment");
        Check(!Game.IsPlaying, area + " boss result pauses game");
        Game.ResumeAfterResult();
        var loot = Game.State.GroundLoot.First(item => item.Id == reward && item.Area == area);
        PositionWorld(new(loot.X, loot.Y));
        Game.InteractNearby();
        Check(Count(reward) == 1, area + " special boss drop can be picked up");
        Game.SaveGame();
        var loaded = SaveStore.Load(out string loadError);
        Check(loaded?.Area == area && loaded.BossesDefeated.Contains(bossKind) && loaded.Activated.Contains(area + "_gate")
            && loaded.DungeonExplored.GetValueOrDefault(area)?.Count > 0, area + " progress, gate and separate fog save: " + loadError);
        InteractObject(area + "_boss_exit");
        await Frames(3);
        Check(Game.State.Area == "world" && Game.Wanderer.Position.DistanceTo(entryPosition + Vector2.Down * 48) < 2,
            area + " completion exit returns to portal and retains character");
        Game.EnterDungeon(area);
        Check(Game.State.Activated.Contains(area + "_gate") && !Game.World.Blocked[38, 22]
            && !Game.Combat.Enemies.Any(enemy => enemy.Kind == bossKind), area + " reentry restores gate and defeated boss stays absent");
        Game.ExitDungeon();
        if (area == "forge") Check(Game.State.RpgCompleted && Game.State.BossKills == 2 && Game.State.Completed, "original relic plus two real bosses completes V0.2 objective");
    }

    private async Task FightBoss(Enemy boss)
    {
        var patterns = new HashSet<int>();
        var attackField = typeof(Enemy).GetField("_selectedAttack", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var directionField = typeof(Enemy).GetField("_attackDirection", BindingFlags.Instance | BindingFlags.NonPublic)!;
        PositionWorld(boss.Position + Vector2.Down * (boss.Kind == "root_boss" ? 130 : 170));
        string previousState = "";
        for (int frame = 0; frame < 850 && patterns.Count < 3; frame++)
        {
            if (boss.AiState == "Windup" && previousState != "Windup")
            {
                patterns.Add((int)attackField.GetValue(boss)!);
                Vector2 direction = (Vector2)directionField.GetValue(boss)!;
                PositionWorld(boss.Position - direction * (boss.Kind == "root_boss" ? 90 : 110));
                Game.Wanderer.TryDodge();
                GD.Print($"ACTION: {boss.Kind} telegraph {(int)attackField.GetValue(boss)!}, HP={boss.Health}");
            }
            previousState = boss.AiState;
            KeepAliveWithPotion();
            await Frames(1);
        }
        Check(patterns.SetEquals(new[] { 0, 1, 2 }) && boss.Health == boss.MaxHealth, boss.Kind + " exposes three real telegraphed patterns before player attacks");
        bool phaseTwo = false;
        for (int frame = 0; frame < 1800 && !Game.State.BossesDefeated.Contains(boss.Kind); frame++)
        {
            if (!Game.IsPlaying) throw new InvalidOperationException(boss.Kind + " player died before combat completion");
            if (boss.PhaseTwo) phaseTwo = true;
            Vector2 offset = Game.Wanderer.Position - boss.Position;
            if (offset.LengthSquared() < 1) offset = Vector2.Down;
            PositionWorld(boss.Position + offset.Normalized() * 58);
            Vector2 target = boss.Position;
            if (frame % 8 == 0)
            {
                Game.Combat.TryAttack(Game.Wanderer.Position.DirectionTo(target));
                for (int i = 0; i < 3; i++) Game.Combat.TrySkill(i, target);
            }
            if (boss.AiState == "Windup" && previousState != "Windup") Game.Wanderer.TryDodge();
            previousState = boss.AiState;
            KeepAliveWithPotion();
            await Frames(1);
        }
        Check(phaseTwo && Game.State.BossesDefeated.Contains(boss.Kind), boss.Kind + " enters second phase and is defeated through TryAttack/TrySkill");
    }

    private async Task VerifySavingAndMigration()
    {
        Game.EquipItem("solar_staff");
        Game.EquipItem("frost_armor");
        Game.EquipItem("tide_charm");
        Position(new(56, 87));
        Game.Combat.DropItem("health_potion", 2, Game.Wanderer.Position + Vector2.Down * 24);
        Game.Pause(); Game.SaveGame();
        string valid = FileAccess.GetFileAsString(SavePath);
        int level = Game.State.Level, inventory = Game.State.Inventory.Sum(item => item.Count);
        int fog = Game.State.Explored.Count, loot = Game.State.GroundLoot.Count;
        Game.ReturnToMenu(); Game.ContinueGame();
        Check(Game.IsPlaying && Game.State.RpgCompleted && Game.State.Level == level && Game.State.Inventory.Sum(item => item.Count) == inventory
            && Game.State.WeaponId == "solar_staff" && Game.State.ArmorId == "frost_armor" && Game.State.AccessoryId == "tide_charm",
            "completed RPG save resumes adventure with actual equipment and inventory");
        Check(Game.State.Explored.Count >= fog && Game.State.GroundLoot.Count == loot && Game.State.BossesDefeated.Count == 2
            && Game.State.LearnedSkills.Count == 3 && Game.State.Passives.Count == 3 && Game.State.Deaths > 0,
            "fog, loot, skills, passives, deaths and boss states persist");
        string export = SaveStore.Export(Game.State);
        using (var document = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(export)))
        {
            Check(document.RootElement.GetProperty("Result").GetString() == "completed"
                && document.RootElement.GetProperty("Stats").GetProperty("BossKills").GetInt32() == 2
                && document.RootElement.GetProperty("Events").GetArrayLength() > 0, "JSON export reports useful real combat results and events");
        }
        Game.Pause();
        WriteSave("{ broken json");
        Check(SaveStore.Load(out string error) == null && error.Length > 0, "malformed external save is rejected");
        Check(!SaveStore.Save(Game.State, out error) && FileAccess.GetFileAsString(SavePath) == "{ broken json", "saving preserves corrupt external file instead of overwriting");
        WriteSave("{\"Version\":2,\"PlayerX\":-1,\"PlayerY\":100}");
        Check(SaveStore.Load(out error) == null && error.Length > 0, "external save with invalid position is rejected");
        int oldIndex = 87 * 120 + 56;
        string legacy = $"{{\"Version\":1,\"Seed\":73129,\"PlayerX\":1808,\"PlayerY\":2800,\"Explored\":[{oldIndex}],\"RuneStep\":1,\"PlayedSeconds\":42}}";
        WriteSave(legacy);
        var migrated = SaveStore.Load(out error);
        Check(migrated != null && migrated.Version == 2 && migrated.RuneStep == 1 && migrated.Explored.Contains(87 * WorldData.Size + 56)
            && migrated.Inventory.Any(item => item.Id == "hunter_bow") && migrated.Checkpoints.Contains("camp") && migrated.Health > 0,
            "minimal V0.1 save migrates old fog, partial puzzle and RPG defaults: " + error);
        WriteSave("{\"Version\":1,\"Seed\":73129,\"PlayerX\":1808,\"PlayerY\":2800,\"Completed\":true,\"RuneStep\":3,\"Mirrors\":[0,2],\"Activated\":[\"bridge\",\"lake\",\"forest\",\"dunes\",\"center\"]}");
        Game.ContinueGame();
        Check(Game.IsPlaying && Game.State.Completed && !Game.State.RpgCompleted && Game.State.LegacyRewardClaimed && Count("tide_charm") == 1,
            "completed V0.1 save resumes new RPG content and claims relic reward once");
        Game.SaveGame(); Game.ContinueGame();
        Check(Count("tide_charm") == 1 && Game.IsPlaying, "migrated relic reward does not duplicate on reload");
        Game.Pause();
        WriteSave(valid);
        Game.ContinueGame();
        Check(Game.State.RpgCompleted && Game.State.BossesDefeated.Count == 2 && Game.IsPlaying, "test fixture restored after external-file checks");
        await Frames(3);
    }

    private void KeepAliveWithPotion()
    {
        if (Game.State.Health <= Game.Stats.MaxHealth * 0.55f && Game.State.Health > 0 && Count("health_potion") > 0)
            Game.UseItem("health_potion");
    }

    private async Task FightNormal(Enemy enemy)
    {
        for (int i = 0; i < 400 && enemy.IsAlive; i++)
        {
            PositionWorld(_combatSpace);
            enemy.Position = _combatSpace + Vector2.Right * 48;
            Game.Combat.TryAttack(Vector2.Right);
            await Frames(1);
        }
        Check(!enemy.IsAlive, "ordinary enemy is defeated by actual sword hits");
    }

    private Vector2 FindCombatSpace(HashSet<Enemy> excluded)
    {
        Vector2 spawn = WorldData.Center(new(56, 87));
        var candidates = new List<Vector2>();
        for (int y = 8; y < Game.World.Height - 8; y++)
        for (int x = 8; x < Game.World.Width - 8; x++)
        {
            var center = WorldData.Center(new(x, y));
            if (center.DistanceTo(spawn) > 1800 || Game.Combat.Enemies.Any(enemy => !excluded.Contains(enemy) && enemy.Position.DistanceTo(center) < 450)) continue;
            bool open = true;
            for (int yy = y - 4; yy <= y + 4 && open; yy++)
            for (int xx = x - 4; xx <= x + 6; xx++)
                if (Game.World.IsBlocked(xx, yy)) { open = false; break; }
            if (open) candidates.Add(center);
        }
        if (candidates.Count == 0) throw new InvalidOperationException("No clear test combat space");
        return candidates.OrderBy(at => at.DistanceSquaredTo(spawn)).First();
    }

    private void Stage(Enemy enemy, float distance)
    {
        PositionWorld(_combatSpace);
        enemy.Position = _combatSpace + Vector2.Right * distance;
        GD.Print($"ACTION: position existing {enemy.Kind} for isolated combat check");
    }

    private void OpenChest(string id)
    {
        int before = Game.State.OpenedChests.Count;
        InteractObject(id);
        Check(Game.State.OpenedChests.Contains(id) && Game.State.OpenedChests.Count == before + 1, "actual chest grants reward " + id);
    }

    private void InteractObject(string id) => InteractAt(Game.World.Objects.Single(obj => obj.Id == id).Tile);
    private void InteractAt(Vector2I tile) { Position(tile); Game.InteractNearby(); }
    private void Position(Vector2I tile) => PositionWorld(WorldData.Center(tile));
    private void PositionWorld(Vector2 at) { Game.Wanderer.Position = at; Game.Wanderer.Velocity = Vector2.Zero; }
    private int Count(string id) => Game.State.Inventory.FirstOrDefault(item => item.Id == id)?.Count ?? 0;

    private async Task WaitForAttack()
    {
        for (int i = 0; i < 90 && Game.Combat.AttackCooldown > 0; i++) await Frames(1);
    }

    private async Task Frames(int count)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private static void WriteSave(string json)
    {
        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        file.StoreString(json);
        file.Flush();
    }

    private static void ReleaseMovement()
    {
        foreach (string action in new[] { "move_left", "move_right", "move_up", "move_down", "run", "attack", "dodge" }) Input.ActionRelease(action);
    }

    private void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        _checks++;
        GD.Print("PASS: " + label);
    }

    private static HashSet<Vector2I> Reachable(WorldData world, Vector2I start)
    {
        var visited = new HashSet<Vector2I> { start };
        var queue = new Queue<Vector2I>();
        queue.Enqueue(start);
        Vector2I[] directions = { Vector2I.Up, Vector2I.Right, Vector2I.Down, Vector2I.Left };
        while (queue.TryDequeue(out var at))
        foreach (var direction in directions)
        {
            var next = at + direction;
            if (!world.IsBlocked(next.X, next.Y) && visited.Add(next)) queue.Enqueue(next);
        }
        return visited;
    }
}
