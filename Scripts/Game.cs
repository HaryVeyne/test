using Godot;
using System;
using System.Linq;
using System.Text.Json;

namespace ProjectUnknown;

public partial class Game : Node2D
{
    public GameState State { get; private set; } = new();
    public WorldData World { get; private set; } = null!;
    public bool HasSession { get; private set; }
    public bool IsPlaying { get; private set; }
    public string CurrentRegion { get; private set; } = "草原";
    public string InteractionPrompt { get; private set; } = "";
    public bool HintsEnabled { get; private set; } = true;
    public float Volume { get; private set; } = 0.65f;
    public DifficultyLevel DefaultDifficulty { get; private set; } = DifficultyLevel.Standard;
    public Player Wanderer => _player;
    public PuzzleController Puzzles => _puzzles;
    public PlayerStats Stats => Progression.Stats(State);
    public CombatController Combat { get; private set; } = null!;
    public DungeonController? Dungeons { get; private set; }
    public string ObjectiveStatus => $"中央遗迹 {(State.Completed ? "已完成" : "未完成")}  ·  地下城 {State.BossesDefeated.Count}/2";

    private WorldRenderer _landscape = null!;
    private DungeonRenderer? _dungeonArt;
    private Player _player = null!;
    private Camera2D _camera = null!;
    private GameUi _ui = null!;
    private PuzzleController _puzzles = null!;
    private Soundscape _sound = null!;
    private double _refreshTimer, _saveTimer, _clock;
    private double _lastStrike = -1;
    private bool _bridgeWasOpen, _quitting;

    public override void _Ready()
    {
        SetupInput();
        LoadSettings();
        GetTree().AutoAcceptQuit = false;
        _sound = new Soundscape();
        AddChild(_sound);
        _sound.SetVolume(Volume);
        BuildWorld(new GameState());
        _camera.Position = WorldData.Center(new Vector2I(60, 59));
        _player.Visible = false;
        _ui = new GameUi();
        AddChild(_ui);
        _ui.Configure(this);
        _ui.ShowMainMenu();
        if (OS.GetCmdlineUserArgs().Contains("--verify"))
            CallDeferred(MethodName.AddChild, new RuntimeVerification { Game = this });
        if (OS.GetCmdlineUserArgs().Contains("--capture-world"))
        {
            StartNew();
            State.Explored.UnionWith(Enumerable.Range(0, World.Width * World.Height));
            _player.Position = WorldData.Center(new Vector2I(60, 64));
            _camera.ResetSmoothing();
        }
    }

    public override void _Process(double delta)
    {
        _clock += delta;
        _landscape.Clock = (float)_clock;
        _landscape.Focus = _camera.GetScreenCenterPosition();
        _landscape.QueueRedraw();
        if (_dungeonArt != null)
        {
            _dungeonArt.Clock = (float)_clock;
            _dungeonArt.Focus = _camera.GetScreenCenterPosition();
            _dungeonArt.QueueRedraw();
        }
        if (!IsPlaying) return;
        if (State.Health <= 0)
        {
            State.Deaths++;
            Record("death", State.Area);
            Pause();
            _ui.ShowDeath();
            return;
        }
        _camera.Position = _player.Position;
        State.PlayedSeconds += delta;
        _refreshTimer += delta;
        _saveTimer += delta;
        if (State.Area == "world")
        {
            var feedback = _puzzles.UpdatePlate(_player.Position, delta);
            _landscape.PlateCharge = _puzzles.PlateCharge;
            if (feedback != null) ShowFeedback(feedback);
        }
        if (_refreshTimer >= 0.15)
        {
            _refreshTimer = 0;
            Explore();
            InteractionPrompt = FindPrompt();
            _ui.Refresh();
        }
        if (_saveTimer >= 30)
        {
            _saveTimer = 0;
            SaveGame(false);
        }
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (input.IsActionPressed("fullscreen"))
        {
            ToggleFullscreen();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (!HasSession || State.Health <= 0) return;
        if (input.IsActionPressed("pause"))
        {
            if (_ui.HasOverlay) _ui.CloseOverlay();
            else _ui.ShowPause();
        }
        else if (input.IsActionPressed("map")) _ui.ToggleMap();
        else if (input.IsActionPressed("journal")) _ui.ToggleJournal();
        else if (input.IsActionPressed("inventory")) _ui.ToggleInventory();
        else if (input.IsActionPressed("character")) _ui.ToggleCharacter();
        else if (IsPlaying && input.IsActionPressed("interact")) InteractNearby();
        else if (IsPlaying && input.IsActionPressed("quick_swap")) QuickSwapWeapon();
        else if (IsPlaying && input.IsActionPressed("quick_heal")) UseItem("health_potion");
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest) QuitGame();
        if (what == NotificationApplicationFocusOut && IsPlaying && _ui != null) _ui.ShowPause();
    }

    public void StartNew()
    {
        if (!SaveStore.ArchiveForNewGame(out string error)) { Notify(error); return; }
        BuildWorld(new GameState { Difficulty = DefaultDifficulty });
        State.Health = Stats.MaxHealth;
        HasSession = true;
        Resume();
        Record("session_start", $"seed={State.Seed};difficulty={State.Difficulty}");
        Note("目标：完成中央遗迹，击败林地墓室和熔炉的守卫。营地可恢复生命并作为复活点。");
        Note("左键攻击，Space 闪避，1 / 2 / 3 使用已学习的技能。I 打开背包，C 学习技能和强化。林地墓室入口位于中央遗迹西北方。");
        Explore();
        _ui.ShowHud();
        Notify("Tab 切换武器，Q 喝药。I 装备，C 学习技能。湿地泵站有新的探索线索。");
        SaveGame(false);
    }

    public void ContinueGame()
    {
        var state = SaveStore.Load(out string error);
        if (state == null) { Notify(error); return; }
        BuildWorld(state);
        HasSession = true;
        Record("session_resume", state.Area);
        if (State.Completed && !State.LegacyRewardClaimed) GiveRelicReward();
        if (State.Health <= 0) { Pause(); _ui.ShowDeath(); return; }
        Resume();
        Explore();
        _ui.ShowHud();
        Notify("存档已载入。");
    }

    public void Pause()
    {
        IsPlaying = false;
        _player.Active = false;
        Combat.CancelAttack();
        if (HasSession) SaveGame(false);
    }

    public void Resume()
    {
        if (!HasSession || State.Health <= 0) return;
        IsPlaying = true;
        _player.Active = true;
    }

    public void ResumeAfterResult() { _ui.ShowHud(); Resume(); }

    public void ReturnToMenu()
    {
        Pause();
        HasSession = false;
        _ui.ShowMainMenu();
    }

    public void SaveGame() => SaveGame(true);

    private void SaveGame(bool notify)
    {
        State.PlayerX = _player.Position.X;
        State.PlayerY = _player.Position.Y;
        if (State.Area == "world") { State.WorldX = State.PlayerX; State.WorldY = State.PlayerY; }
        bool saved = SaveStore.Save(State, out string error);
        if (!saved) Notify(error);
        else if (notify) Notify("已保存。", true);
    }

    public void ExportLog()
    {
        State.PlayerX = _player.Position.X;
        State.PlayerY = _player.Position.Y;
        string path = SaveStore.Export(State);
        Notify(path.StartsWith("导出失败", StringComparison.Ordinal) ? path : $"记录已导出\n{path}", !path.StartsWith("导出失败", StringComparison.Ordinal));
    }

    public void RequestHint()
    {
        if (!HintsEnabled) { Notify("提示已关闭。"); return; }
        string hint = State.Area != "world" ? State.Area == "crypt"
            ? "支路的供能台可以改变主门状态。检查门边的两盏指示灯。"
            : "热地板会在喷发前亮起。冷却阀位于侧面的管道房。"
            : _puzzles.Hint();
        Note(hint);
        Notify(hint);
        _ui.Refresh();
    }

    public async void QuitGame()
    {
        if (_quitting) return;
        _quitting = true;
        Pause();
        _sound.Stop();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit();
    }

    public void ToggleFullscreen()
    {
        var mode = DisplayServer.WindowGetMode();
        DisplayServer.WindowSetMode(mode == DisplayServer.WindowMode.Fullscreen ? DisplayServer.WindowMode.Windowed : DisplayServer.WindowMode.Fullscreen);
    }

    public void SetVolume(float value) { Volume = Mathf.Clamp(value, 0, 1); _sound.SetVolume(Volume); SaveSettings(); }
    public void SetHints(bool enabled) { HintsEnabled = enabled; SaveSettings(); }
    public void Notify(string text, bool success = false) { _ui?.Toast(text, success); }
    public void Record(string action, string detail) => State.Events.Add(new(State.PlayedSeconds, action, detail));
    public void Note(string text) { if (!State.Journal.Any(n => n.Text == text)) State.Journal.Add(new(State.PlayedSeconds, text)); }

    public bool SetDifficulty(DifficultyLevel difficulty)
    {
        if (HasSession && Combat.HasThreatNearby) { Notify("战斗中无法切换难度。"); return false; }
        DefaultDifficulty = difficulty;
        if (HasSession)
        {
            State.Difficulty = difficulty;
            foreach (var enemy in Combat.Enemies) enemy.ApplyDifficulty();
            Record("difficulty_changed", difficulty.ToString());
            SaveGame(false);
        }
        SaveSettings();
        Notify("难度：" + CombatRules.Profile(difficulty).Name, true);
        return true;
    }

    public void QuickSwapWeapon()
    {
        string reserve = State.ReserveWeaponId;
        if (reserve.Length == 0 || !State.Inventory.Any(item => item.Id == reserve))
            reserve = State.Inventory.FirstOrDefault(item => Items.Get(item.Id).Kind == ItemKind.Weapon && item.Id != State.WeaponId)?.Id ?? "";
        if (reserve.Length == 0) { Notify("没有可切换的武器。"); return; }
        EquipItem(reserve);
    }

    public void EquipItem(string id)
    {
        bool changed = Items.Equip(State, id, out string message);
        if (changed) { Combat.CancelAttack(); Record("equip", id); }
        Notify(message, changed);
        _ui.Refresh();
    }

    public void UnequipItem(string slot)
    {
        bool changed = Items.Unequip(State, slot, out string message);
        if (changed) { Combat.CancelAttack(); Record("unequip", slot); }
        Notify(message, changed);
        _ui.Refresh();
    }

    public void UseItem(string id)
    {
        bool used = Items.Use(State, id, out string message);
        if (used) Record("consume", id);
        Notify(message, used);
        _ui.Refresh();
    }

    public void DropItem(string id)
    {
        if (id == State.WeaponId || id == State.ArmorId || id == State.AccessoryId) { Notify("请先卸下这件装备。"); return; }
        if (!Items.Remove(State, id)) return;
        if (State.ReserveWeaponId == id && !State.Inventory.Any(item => item.Id == id)) State.ReserveWeaponId = "";
        Combat.DropItem(id, 1, _player.Position + Vector2.Down * 20);
        Record("discard", id);
        Notify("已丢弃 " + Items.Get(id).Name);
        _ui.Refresh();
    }

    public void LearnSkill(string id)
    {
        bool learned = Progression.Learn(State, id, out string message);
        if (learned) Record("upgrade", id);
        Notify(message, learned);
        _ui.Refresh();
    }

    public void InteractNearby()
    {
        if (Combat.PickUpNearest()) return;
        var obj = NearestObject();
        if (obj != null)
        {
            State.InteractionCount++;
            Record("interact_object", obj.Id);
            switch (obj.Kind)
            {
                case "checkpoint":
                    if (Combat.HasThreatNearby) { Notify("附近有敌人，无法休息。"); return; }
                    State.Checkpoints.Add(obj.Id);
                    State.CheckpointId = obj.Id;
                    State.Health = Stats.MaxHealth;
                    Notify("已恢复生命并设置复活点：" + obj.Name, true);
                    SaveGame(false);
                    return;
                case "chest":
                    if (!State.OpenedChests.Add(obj.Id)) { Notify("宝箱已打开。"); return; }
                    Items.Add(State, obj.Value, obj.Value == "health_potion" ? 2 : 1);
                    Record("chest", obj.Id + ":" + obj.Value);
                    Notify("获得 " + Items.Get(obj.Value).Name, true);
                    _sound.Chime(true);
                    return;
                case "portal": EnterDungeon(obj.Value); return;
                case "exit": ExitDungeon(); return;
                default:
                    if (Wetland.Interact(this, obj, out var wetlandFeedback)) { ShowFeedback(wetlandFeedback); SaveGame(false); return; }
                    if (Dungeons != null && Dungeons.Interact(obj, out var feedback)) { ShowFeedback(feedback); return; }
                    break;
            }
        }
        if (State.Area != "world") return;
        string? id = _puzzles.Nearest(_player.Position);
        if (id != null) ShowFeedback(_puzzles.Interact(id));
    }

    private WorldObject? NearestObject() => World.Objects.Where(o => WorldData.Center(o.Tile).DistanceSquaredTo(_player.Position) <= 48 * 48)
        .OrderBy(o => WorldData.Center(o.Tile).DistanceSquaredTo(_player.Position)).FirstOrDefault();

    private string FindPrompt()
    {
        if (Combat.LootPrompt.Length > 0) return Combat.LootPrompt;
        var obj = NearestObject();
        if (obj != null) return obj.Kind switch
        {
            "checkpoint" => "E · 休息 / 设置复活点",
            "chest" => State.OpenedChests.Contains(obj.Id) ? "宝箱已打开" : "E · 打开宝箱",
            "portal" => "E · 进入" + obj.Name,
            "exit" => "E · 返回地表",
            _ => "E · " + obj.Name
        };
        string? id = State.Area == "world" ? _puzzles.Nearest(_player.Position) : null;
        return id == null ? "" : _puzzles.Prompt(id);
    }

    public void EnterDungeon(string area)
    {
        if (State.Area != "world" || area is not ("crypt" or "forge")) return;
        State.WorldX = _player.Position.X;
        State.WorldY = _player.Position.Y;
        BuildArea(area, null);
        Resume();
        Explore();
        _ui.ShowHud();
        Record("area_enter", area);
        Notify(area == "crypt" ? "林地墓室：开启供能台，进入守卫大厅。" : "熔炉：关闭热地板，进入核心大厅。");
        SaveGame(false);
    }

    public void ExitDungeon()
    {
        if (State.Area == "world") return;
        string oldArea = State.Area;
        var at = new Vector2(State.WorldX, State.WorldY) + Vector2.Down * 48;
        BuildArea("world", at);
        Resume();
        Explore();
        _ui.ShowHud();
        Record("area_exit", oldArea);
        SaveGame(false);
    }

    public void RefreshTerrain()
    {
        _landscape.RefreshCollisions();
        Combat.RefreshNavigation();
    }

    public void TravelTo(string id)
    {
        if (State.Area != "world" || Combat.HasThreatNearby) { Notify("地下城或战斗中无法快速旅行。"); return; }
        if (!State.Checkpoints.Contains(id)) return;
        var checkpoint = World.Objects.FirstOrDefault(o => o.Kind == "checkpoint" && o.Id == id);
        if (checkpoint == null) return;
        _player.Position = WorldData.Center(checkpoint.Tile) + Vector2.Down * 40;
        State.CheckpointId = id;
        State.Health = Stats.MaxHealth;
        _camera.Position = _player.Position;
        _camera.ResetSmoothing();
        Record("travel", id);
        ResumeAfterResult();
        Explore();
        Notify("已到达 " + checkpoint.Name, true);
        SaveGame(false);
    }

    public void Respawn()
    {
        var surface = new WorldData(State.Seed);
        var checkpoint = surface.Objects.First(o => o.Kind == "checkpoint" && o.Id == State.CheckpointId);
        State.Health = Stats.MaxHealth;
        BuildArea("world", WorldData.Center(checkpoint.Tile) + Vector2.Down * 40);
        Combat.ResetPlayerCombat();
        Resume();
        Explore();
        _ui.ShowHud();
        Record("respawn", State.CheckpointId);
        SaveGame(false);
    }

    public void OnPlayerHit(int damage)
    {
        State.DamageTaken += damage;
        Record("player_hit", $"{State.Area}:damage={damage};hp={State.Health:0}");
        _sound.CombatHit(false);
    }

    public void OnEnemyHit(int damage)
    {
        if (_clock - _lastStrike < 0.08) return;
        _lastStrike = _clock;
        _sound.CombatHit(true);
    }

    public void OnEnemyKilled(Enemy enemy)
    {
        if (!State.KilledEnemies.Add(enemy.SpawnId)) return;
        State.EnemyKills++;
        int gained = Progression.GrantExperience(State, enemy.ExperienceReward);
        Record("enemy_defeated", $"{State.Area}:{enemy.SpawnId}:{enemy.Kind};xp={enemy.ExperienceReward}");
        if (gained > 0) Notify($"升至 {State.Level} 级，技能点 +{gained}。", true);
        if (enemy.SpawnId == "marsh_keeper")
        {
            State.Activated.Add("marsh_complete");
            Combat.DropItem("sluice_staff", 1, enemy.Position);
            Note("库房守卫已击败，掉落水闸法杖：奥术伤害对重甲更有效。库房宝箱中还有薄霜护符。");
            Notify("库房守卫已击败，掉落：水闸法杖。", true);
            SaveGame(false);
        }
        else if (enemy.IsBoss)
        {
            State.BossesDefeated.Add(enemy.Kind);
            State.BossKills++;
            string reward = enemy.Kind == "root_boss" ? "guardian_blade" : "solar_staff";
            Combat.DropItem(reward, 1, enemy.Position);
            Combat.DropItem("health_potion", 3, enemy.Position + Vector2.Down * 25);
            Dungeons?.OnBossDefeated(enemy.Kind);
            Note((enemy.Kind == "root_boss" ? "林地墓室" : "熔炉") + "已完成。特殊装备已掉落，出口位于大厅后方。");
            SaveGame(false);
            if (State.Health <= 0) return;
            if (!CheckGoal())
            {
                Pause();
                _ui.ShowMilestone(enemy.Kind == "root_boss" ? "林地墓室完成" : "熔炉完成", $"经验 +{enemy.ExperienceReward}；掉落：{Items.Get(reward).Name}、生命药水 ×3。");
            }
        }
        else if (enemy.Kind == "elite") Combat.DropItem("copper_ring", 1, enemy.Position);
        else if (State.EnemyKills % 3 == 1) Combat.DropItem("health_potion", 1, enemy.Position);
        if (gained > 0 || enemy.IsBoss) _sound.Chime(true);
    }

    private bool CheckGoal()
    {
        if (State.RpgCompleted || !State.Completed || State.BossesDefeated.Count < 2) return false;
        State.RpgCompleted = true;
        Record("rpg_complete", "central relic + crypt + forge");
        SaveGame(false);
        Pause();
        _ui.ShowEnding();
        return true;
    }

    private void GiveRelicReward()
    {
        State.LegacyRewardClaimed = true;
        Items.Add(State, "tide_charm", 1);
        Progression.GrantExperience(State, 160);
        State.SkillPoints += 2;
        Note("中央遗迹已完成：获得潮汐护符、160 经验和 2 技能点。墓室和熔炉的守卫仍需击败。");
        Record("relic_reward", "tide_charm;xp160;skillpoints2");
    }

    private void ShowFeedback(PuzzleFeedback feedback)
    {
        Notify(feedback.Message, feedback.Success);
        _sound.Chime(feedback.Success);
        bool bridgeOpen = State.Activated.Contains("bridge");
        if (State.Area == "world" && bridgeOpen != _bridgeWasOpen)
        {
            _bridgeWasOpen = bridgeOpen;
            World.OpenBridge();
            RefreshTerrain();
        }
        if (State.Completed && !State.LegacyRewardClaimed)
        {
            GiveRelicReward();
            SaveGame(false);
            if (!CheckGoal()) { Pause(); _ui.ShowMilestone("中央遗迹完成", "获得潮汐护符、160 经验、2 技能点。可继续探索地下城。"); }
        }
    }

    private void BuildWorld(GameState state)
    {
        State = state;
        _puzzles = new PuzzleController(State);
        BuildArea(State.Area, State.PlayerX == 0 && State.PlayerY == 0 ? null : new Vector2(State.PlayerX, State.PlayerY));
    }

    private static void RemoveRuntime(Node? node)
    {
        if (node == null) return;
        node.GetParent().RemoveChild(node);
        node.QueueFree();
    }

    private void BuildArea(string area, Vector2? position)
    {
        IsPlaying = false;
        RemoveRuntime(Combat);
        RemoveRuntime(Dungeons);
        RemoveRuntime(_dungeonArt);
        RemoveRuntime(_landscape);
        RemoveRuntime(_player);
        RemoveRuntime(_camera);
        Dungeons = null;
        _dungeonArt = null;
        State.Area = area;
        World = new WorldData(State.Seed, area);
        Wetland.Apply(World, State);
        _bridgeWasOpen = State.Activated.Contains("bridge");
        if (area == "world" && _bridgeWasOpen) World.OpenBridge();
        if (area != "world" && State.Activated.Contains(area + "_gate")) World.OpenGate(area + "_gate");
        _landscape = new WorldRenderer();
        AddChild(_landscape);
        _landscape.Configure(World, State);
        Vector2 spawn = position ?? World.Spawn;
        if (World.IsBlocked((int)(spawn.X / 32), (int)(spawn.Y / 32))) spawn = World.Spawn;
        _player = new Player { Position = spawn };
        AddChild(_player);
        _player.Configure(this);
        _player.Travelled += (distance, started) => { State.Distance += distance; if (started) State.MoveCount++; };
        State.PlayerX = spawn.X;
        State.PlayerY = spawn.Y;
        _camera = new Camera2D { Position = spawn, PositionSmoothingEnabled = true,
            PositionSmoothingSpeed = 7, Zoom = new Vector2(1.5f, 1.5f), LimitLeft = 0, LimitTop = 0,
            LimitRight = World.Width * 32, LimitBottom = World.Height * 32 };
        AddChild(_camera);
        _camera.MakeCurrent();
        _camera.ResetSmoothing();
        Combat = new CombatController();
        AddChild(Combat);
        Combat.Configure(this);
        if (area != "world")
        {
            Dungeons = new DungeonController();
            AddChild(Dungeons);
            Dungeons.Configure(this);
            _dungeonArt = new DungeonRenderer();
            AddChild(_dungeonArt);
            _dungeonArt.Configure(World, State);
        }
        _saveTimer = _refreshTimer = 0;
        InteractionPrompt = "";
    }

    private void Explore()
    {
        var tile = new Vector2I((int)(_player.Position.X / 32), (int)(_player.Position.Y / 32));
        var fog = State.ExploredFor(State.Area);
        for (int y = Math.Max(0, tile.Y - 8); y <= Math.Min(World.Height - 1, tile.Y + 8); y++)
        for (int x = Math.Max(0, tile.X - 8); x <= Math.Min(World.Width - 1, tile.X + 8); x++)
            if ((x - tile.X) * (x - tile.X) + (y - tile.Y) * (y - tile.Y) <= 64) fog.Add(y * World.Width + x);
        CurrentRegion = State.Area switch
        {
            "crypt" => "林地墓室", "forge" => "熔炉", _ => World.TileAt(tile.X, tile.Y) switch
            {
                Terrain.Forest => "森林", Terrain.Dunes => "荒漠", Terrain.Wetland => "湿地", Terrain.Snow => "雪地",
                Terrain.Water => "湖畔", Terrain.Ruins => "遗迹", _ => "草原"
            }
        };
        if (State.Regions.Add(CurrentRegion)) { Record("region_discovered", CurrentRegion); Note("发现区域：" + CurrentRegion); }
        foreach (var landmark in World.Landmarks)
            if (_player.Position.DistanceTo(WorldData.Center(landmark.Tile)) <= landmark.Radius * 32 && !State.Discovered.Contains(landmark.Id))
            {
                _puzzles.Discover(landmark.Id, landmark.Name);
                Notify("发现 " + landmark.Name);
            }
    }

    private static void SetupInput()
    {
        Bind("move_left", Key.A, Key.Left); Bind("move_right", Key.D, Key.Right);
        Bind("move_up", Key.W, Key.Up); Bind("move_down", Key.S, Key.Down);
        Bind("run", Key.Shift); Bind("dodge", Key.Space);
        Bind("quick_swap", Key.Tab); Bind("quick_heal", Key.Q);
        Bind("interact", Key.E); Bind("inventory", Key.I); Bind("character", Key.C);
        Bind("map", Key.M); Bind("journal", Key.J); Bind("pause", Key.Escape); Bind("fullscreen", Key.F11);
        Bind("skill_1", Key.Key1); Bind("skill_2", Key.Key2); Bind("skill_3", Key.Key3);
        if (!InputMap.HasAction("attack")) InputMap.AddAction("attack");
        InputMap.ActionAddEvent("attack", new InputEventMouseButton { ButtonIndex = MouseButton.Left });
        if (!InputMap.HasAction("charge_attack")) InputMap.AddAction("charge_attack");
        InputMap.ActionAddEvent("charge_attack", new InputEventMouseButton { ButtonIndex = MouseButton.Right });
    }

    private static void Bind(string action, params Key[] keys)
    {
        if (InputMap.HasAction(action)) InputMap.EraseAction(action);
        InputMap.AddAction(action);
        foreach (var key in keys) InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
    }

    private void LoadSettings()
    {
        if (!FileAccess.FileExists("user://settings.json")) return;
        try
        {
            var settings = JsonSerializer.Deserialize<Settings>(FileAccess.GetFileAsString("user://settings.json"));
            if (settings != null)
            {
                Volume = Mathf.Clamp(settings.Volume, 0, 1); HintsEnabled = settings.HintsEnabled;
                if (Enum.IsDefined(settings.Difficulty)) DefaultDifficulty = settings.Difficulty;
            }
        }
        catch (JsonException) { GD.Print("Invalid settings JSON; defaults loaded."); }
    }

    private void SaveSettings()
    {
        using var file = FileAccess.Open("user://settings.json", FileAccess.ModeFlags.Write);
        if (file != null) file.StoreString(JsonSerializer.Serialize(new Settings { Volume = Volume, HintsEnabled = HintsEnabled, Difficulty = DefaultDifficulty }));
    }

    private sealed class Settings
    {
        public float Volume { get; set; } = 0.65f;
        public bool HintsEnabled { get; set; } = true;
        public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Standard;
    }
}
