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
    public string CurrentRegion { get; private set; } = "无名草原";
    public string InteractionPrompt { get; private set; } = "";
    public bool HintsEnabled { get; private set; } = true;
    public float Volume { get; private set; } = 0.65f;
    public Player Wanderer => _player;
    public PuzzleController Puzzles => _puzzles;

    private WorldRenderer _landscape = null!;
    private Player _player = null!;
    private Camera2D _camera = null!;
    private GameUi _ui = null!;
    private PuzzleController _puzzles = null!;
    private Soundscape _sound = null!;
    private double _refreshTimer;
    private double _saveTimer;
    private double _clock;
    private string? _nearest;
    private bool _bridgeWasOpen;
    private bool _quitting;

    public override void _Ready()
    {
        SetupInput();
        LoadSettings();
        GetTree().AutoAcceptQuit = false;
        _sound = new Soundscape();
        AddChild(_sound);
        _sound.SetVolume(Volume);
        BuildWorld(new GameState());
        foreach (int index in Enumerable.Range(0, WorldData.Size * WorldData.Size)) State.Explored.Add(index);
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
            State.Explored.UnionWith(Enumerable.Range(0, WorldData.Size * WorldData.Size));
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
        if (!IsPlaying) return;
        _camera.Position = _player.Position;
        State.PlayedSeconds += delta;
        _refreshTimer += delta;
        _saveTimer += delta;
        var feedback = _puzzles.UpdatePlate(_player.Position, delta);
        _landscape.PlateCharge = _puzzles.PlateCharge;
        if (feedback != null) ShowFeedback(feedback);
        if (_refreshTimer >= 0.15)
        {
            _refreshTimer = 0;
            Explore();
            _nearest = _puzzles.Nearest(_player.Position);
            InteractionPrompt = _nearest == null ? "" : _puzzles.Prompt(_nearest);
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
        if (!HasSession || State.Completed) return;
        if (input.IsActionPressed("pause"))
        {
            if (_ui.HasOverlay) _ui.CloseOverlay();
            else _ui.ShowPause();
        }
        else if (input.IsActionPressed("map")) _ui.ToggleMap();
        else if (input.IsActionPressed("journal")) _ui.ToggleJournal();
        else if (IsPlaying && input.IsActionPressed("interact")) InteractNearby();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest) QuitGame();
        if (what == NotificationApplicationFocusOut && IsPlaying && _ui != null) _ui.ShowPause();
    }

    public void StartNew()
    {
        if (!SaveStore.ArchiveForNewGame(out string error))
        {
            _ui.Toast(error);
            return;
        }
        var state = new GameState();
        BuildWorld(state);
        HasSession = true;
        IsPlaying = true;
        _player.Active = true;
        _puzzles.Record("session_start", $"seed={State.Seed}");
        State.Journal.Add(new JournalEntry(0, "我在一处熄灭的营地醒来。风里似乎有低沉的回声，远处的断壁上立着一道没有门的拱。"));
        Explore();
        _ui.ShowHud();
        _ui.Toast("风还记得这里。试着走近，试着聆听。");
        SaveGame(false);
    }

    public void ContinueGame()
    {
        var state = SaveStore.Load(out string error);
        if (state == null)
        {
            _ui.Toast(error);
            return;
        }
        BuildWorld(state);
        HasSession = true;
        _puzzles.Record("session_resume", "本地存档恢复");
        if (state.Completed)
        {
            IsPlaying = false;
            _ui.ShowEnding();
        }
        else
        {
            Resume();
            _ui.ShowHud();
            Explore();
            _ui.Toast("回到上一次的足迹。");
        }
    }

    public void Pause()
    {
        IsPlaying = false;
        _player.Active = false;
        if (HasSession) SaveGame(false);
    }

    public void Resume()
    {
        if (!HasSession || State.Completed) return;
        IsPlaying = true;
        _player.Active = true;
    }

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
        bool saved = SaveStore.Save(State, out string error);
        if (!saved) _ui.Toast(error);
        else if (notify) _ui.Toast("足迹已保存。", true);
    }

    public void ExportLog()
    {
        State.PlayerX = _player.Position.X;
        State.PlayerY = _player.Position.Y;
        string path = SaveStore.Export(State);
        if (path.StartsWith("导出失败", StringComparison.Ordinal)) _ui.Toast(path);
        else _ui.Toast($"探索记录已导出\n{path}", true);
    }

    public void RequestHint()
    {
        if (!HintsEnabled) { _ui.Toast("渐进提示已关闭，可在设置中开启。"); return; }
        string hint = _puzzles.Hint();
        State.Journal.Add(new JournalEntry(State.PlayedSeconds, $"思索：{hint}"));
        _ui.Toast(hint);
        _ui.Refresh();
    }

    public async void QuitGame()
    {
        if (_quitting) return;
        _quitting = true;
        if (HasSession) SaveGame(false);
        _sound.Stop();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit();
    }

    public void ToggleFullscreen()
    {
        var mode = DisplayServer.WindowGetMode();
        DisplayServer.WindowSetMode(mode == DisplayServer.WindowMode.Fullscreen
            ? DisplayServer.WindowMode.Windowed : DisplayServer.WindowMode.Fullscreen);
    }

    public void SetVolume(float value)
    {
        Volume = Mathf.Clamp(value, 0, 1);
        _sound.SetVolume(Volume);
        SaveSettings();
    }

    public void SetHints(bool enabled) { HintsEnabled = enabled; SaveSettings(); }

    public void InteractNearby()
    {
        string? id = _puzzles.Nearest(_player.Position);
        if (id == null) return;
        ShowFeedback(_puzzles.Interact(id));
    }

    private void ShowFeedback(PuzzleFeedback feedback)
    {
        _ui.Toast(feedback.Message, feedback.Success);
        _sound.Chime(feedback.Success);
        bool bridgeOpen = State.Activated.Contains("bridge");
        if (bridgeOpen != _bridgeWasOpen)
        {
            _bridgeWasOpen = bridgeOpen;
            World.OpenBridge();
            _landscape.RefreshCollisions();
        }
        if (State.Completed)
        {
            IsPlaying = false;
            _player.Active = false;
            SaveGame(false);
            _ui.ShowEnding();
        }
    }

    private void BuildWorld(GameState state)
    {
        if (_landscape != null) { RemoveChild(_landscape); _landscape.QueueFree(); }
        if (_player != null) { RemoveChild(_player); _player.QueueFree(); }
        if (_camera != null) { RemoveChild(_camera); _camera.QueueFree(); }
        State = state;
        World = new WorldData(State.Seed);
        _puzzles = new PuzzleController(State);
        _bridgeWasOpen = State.Activated.Contains("bridge");
        if (_bridgeWasOpen) World.OpenBridge();
        _landscape = new WorldRenderer();
        AddChild(_landscape);
        _landscape.Configure(World, State);
        _player = new Player { Position = State.PlayerX == 0 && State.PlayerY == 0
            ? World.Spawn : new Vector2(State.PlayerX, State.PlayerY) };
        AddChild(_player);
        _player.Travelled += (distance, started) =>
        {
            State.Distance += distance;
            if (started) State.MoveCount++;
        };
        _camera = new Camera2D { Position = _player.Position, PositionSmoothingEnabled = true,
            PositionSmoothingSpeed = 5, Zoom = new Vector2(1.5f, 1.5f),
            LimitLeft = 0, LimitTop = 0, LimitRight = WorldData.Size * WorldData.TileSize,
            LimitBottom = WorldData.Size * WorldData.TileSize };
        AddChild(_camera);
        _camera.MakeCurrent();
        _camera.ResetSmoothing();
        _saveTimer = 0;
        _refreshTimer = 0;
        InteractionPrompt = "";
    }

    private void Explore()
    {
        _camera.Position = _player.Position;
        var tile = new Vector2I((int)(_player.Position.X / WorldData.TileSize), (int)(_player.Position.Y / WorldData.TileSize));
        for (int y = Math.Max(0, tile.Y - 8); y <= Math.Min(WorldData.Size - 1, tile.Y + 8); y++)
        for (int x = Math.Max(0, tile.X - 8); x <= Math.Min(WorldData.Size - 1, tile.X + 8); x++)
            if ((x - tile.X) * (x - tile.X) + (y - tile.Y) * (y - tile.Y) <= 64) State.Explored.Add(y * WorldData.Size + x);
        string region = World.TileAt(tile.X, tile.Y) switch
        {
            Terrain.Forest => "低语森林",
            Terrain.Dunes => "镜沙荒漠",
            Terrain.Water => "回声湖畔",
            Terrain.Ruins => "无名遗迹",
            _ => "风息草原"
        };
        if (_player.Position.DistanceTo(WorldData.Center(new Vector2I(27, 82))) < 260) region = "回声湖畔";
        CurrentRegion = region;
        if (State.Regions.Add(region))
        {
            _puzzles.Record("region_discovered", region);
            State.Journal.Add(new JournalEntry(State.PlayedSeconds, $"踏入{region}。这一带的风和色彩都变了。"));
            _ui?.Toast($"发现区域 · {region}");
        }
        foreach (var landmark in World.Landmarks)
        {
            if (_player.Position.DistanceTo(WorldData.Center(landmark.Tile)) <= landmark.Radius * WorldData.TileSize
                && !State.Discovered.Contains(landmark.Id))
            {
                _puzzles.Discover(landmark.Id, landmark.Name);
                _ui?.Toast($"记下地点 · {landmark.Name}");
            }
        }
    }

    private static void SetupInput()
    {
        Bind("move_left", Key.A, Key.Left);
        Bind("move_right", Key.D, Key.Right);
        Bind("move_up", Key.W, Key.Up);
        Bind("move_down", Key.S, Key.Down);
        Bind("run", Key.Shift);
        Bind("interact", Key.E);
        Bind("map", Key.M);
        Bind("journal", Key.J);
        Bind("pause", Key.Escape);
        Bind("fullscreen", Key.F11);
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
            if (settings != null) { Volume = Mathf.Clamp(settings.Volume, 0, 1); HintsEnabled = settings.HintsEnabled; }
        }
        catch (JsonException) { GD.Print("Settings file invalid; using defaults."); }
    }

    private void SaveSettings()
    {
        using var file = FileAccess.Open("user://settings.json", FileAccess.ModeFlags.Write);
        if (file != null) file.StoreString(JsonSerializer.Serialize(new Settings { Volume = Volume, HintsEnabled = HintsEnabled }));
    }

    private sealed class Settings
    {
        public float Volume { get; set; } = 0.65f;
        public bool HintsEnabled { get; set; } = true;
    }
}
