using Godot;
using System.Linq;
using System.Collections.Generic;

namespace ProjectUnknown;

public partial class DungeonController : Node
{
    private Game _game = null!;
    private long _lastHeatCycle = -1;
    private readonly HashSet<Enemy> _heated = new();
    private bool _playerHeated;

    public bool BossAccessible => _game.World.Area == "world"
        || _game.State.Activated.Contains($"{_game.World.Area}_gate");
    public static float HeatPhase(double seconds) => (float)(seconds % 4.5);
    public static bool HeatWarning(double seconds) => HeatPhase(seconds) >= 3.0f && HeatPhase(seconds) < 3.8f;
    public static bool HeatActive(double seconds) => HeatPhase(seconds) >= 3.8f && HeatPhase(seconds) < 4.25f;

    public void Configure(Game game)
    {
        _game = game;
        if (game.World.Area == "crypt" && game.State.Activated.Contains("crypt_supply_a") && game.State.Activated.Contains("crypt_supply_b"))
            game.State.Activated.Add("crypt_gate");
        if (game.World.Area == "forge" && game.State.Activated.Contains("forge_coolant"))
            game.State.Activated.Add("forge_gate");
        string gate = $"{game.World.Area}_gate";
        if (game.State.Activated.Contains(gate))
        {
            bool closed = game.World.Blocked[38, 22];
            game.World.OpenGate(gate);
            if (closed) game.RefreshTerrain();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_game.IsPlaying || _game.World.Area != "forge" || _game.State.Activated.Contains("forge_coolant")) return;
        long cycle = (long)(_game.State.PlayedSeconds / 4.5);
        if (_lastHeatCycle != cycle) { _lastHeatCycle = cycle; _heated.Clear(); _playerHeated = false; }
        if (!HeatActive(_game.State.PlayedSeconds)) return;
        if (!_playerHeated && DungeonLayout.HeatFloor.HasPoint(_game.Wanderer.Position))
        {
            _playerHeated = true;
            _game.Combat.DamagePlayer(20, DungeonLayout.HeatFloor.GetCenter());
        }
        foreach (var enemy in _game.Combat.Enemies.ToArray())
            if (enemy.IsAlive && DungeonLayout.HeatFloor.HasPoint(enemy.Position) && _heated.Add(enemy))
                enemy.TakeDamage(36, Vector2.Zero, DamageKind.Environment);
    }

    public bool Interact(WorldObject obj, out PuzzleFeedback feedback)
    {
        feedback = new("");
        if (obj.Kind is not ("switch" or "shrine")) return false;
        if (obj.Kind == "shrine")
        {
            Note(obj.Value);
            feedback = new(obj.Value);
            return true;
        }
        if (!_game.State.Activated.Add(obj.Id))
        {
            feedback = new(obj.Id == "forge_coolant" ? "冷却系统已开启。" : "供能台已启动。", true);
            return true;
        }
        _game.Puzzles.Record("mechanism", obj.Id);
        if (obj.Id == "forge_coolant")
        {
            OpenGate("forge_gate");
            Note("冷却阀已开启：热地板停止升温，核心机房的门锁解除。");
            feedback = new("冷却系统开启。热地板已关闭，东侧闸门已打开。", true, true);
        }
        else if (_game.State.Activated.Contains("crypt_supply_a") && _game.State.Activated.Contains("crypt_supply_b"))
        {
            OpenGate("crypt_gate");
            Note("南北供能台已接通，东侧守卫墓室开门。");
            feedback = new("两路供能已接通。东侧石门已打开。", true, true);
        }
        else
        {
            Note($"{obj.Name}已启动。门锁仍缺一路供能。");
            feedback = new($"{obj.Name}已启动。门锁仍缺一路供能。", true, true);
        }
        return true;
    }

    private void OpenGate(string id)
    {
        _game.State.Activated.Add(id);
        _game.World.OpenGate(id);
        _game.RefreshTerrain();
    }

    public void OnBossDefeated(string kind)
    {
        string area = kind == "root_boss" ? "crypt" : "forge";
        if (!_game.State.Activated.Add($"{area}_complete")) return;
        Note(area == "crypt" ? "林地墓室已完成：守卫击败，墓室出口可返回地表。" : "地下铸炉已完成：核心机甲击败，机房出口可返回地表。");
        _game.Puzzles.Record("dungeon_complete", area);
    }

    private void Note(string text)
    {
        if (!_game.State.Journal.Any(entry => entry.Text == text))
            _game.State.Journal.Add(new(_game.State.PlayedSeconds, text));
    }
}
