using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectUnknown;

public partial class CombatController : Node2D
{
    public Game Game { get; private set; } = null!;
    public List<Enemy> Enemies { get; } = new();
    public float[] SkillCooldowns { get; } = new float[3];
    public float AttackCooldown { get; private set; }
    public bool HasThreatNearby => Enemies.Any(enemy => enemy.IsAlive && enemy.IsAccessible
        && (enemy.GlobalPosition.DistanceSquaredTo(Game.Wanderer.GlobalPosition) < 420 * 420 || enemy.AiState is "Windup" or "Attack"))
        || _projectiles.Any(p => p.EnemyProjectile && p.Position.DistanceSquaredTo(Game.Wanderer.Position) < 500 * 500);
    public float ChargeProgress => _charging ? Mathf.Clamp(_chargeTime / .7f, 0, 1) : 0;
    public float DodgeEmpowerTime { get; private set; }
    public string LootPrompt
    {
        get
        {
            var loot = NearestLoot();
            return loot == null ? "" : $"E 拾取 {Items.Get(loot.Id).Name} ×{loot.Count}";
        }
    }

    private AStarGrid2D _navigation = new();
    private Font _combatFont = null!;
    private readonly List<Visual> _visuals = new();
    private readonly List<Projectile> _projectiles = new();
    private readonly HashSet<Enemy> _swordHits = new();
    private Attack? _attack;
    private float _time;
    private int _combo;
    private float _lastSwordTime = -10;
    private bool _charging;
    private float _chargeTime;

    public void Configure(Game game)
    {
        Game = game;
        _combatFont = GD.Load<Font>("res://Assets/Fonts/NotoSansSC-Regular.otf");
        ZIndex = 18;
        RefreshNavigation();
        foreach (var spawn in Game.World.EnemySpawns)
        {
            if (Game.State.KilledEnemies.Contains(spawn.Id)) continue;
            var enemy = new Enemy { Position = WorldData.Center(spawn.Tile) };
            enemy.Configure(this, spawn);
            Enemies.Add(enemy);
            AddChild(enemy);
        }
    }

    public override void _ExitTree() => _navigation.Dispose();

    public void RefreshNavigation()
    {
        _navigation.Region = new Rect2I(0, 0, Game.World.Width, Game.World.Height);
        _navigation.CellSize = new Vector2(WorldData.TileSize, WorldData.TileSize);
        _navigation.Offset = Vector2.One * (WorldData.TileSize / 2f);
        _navigation.DiagonalMode = AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles;
        _navigation.DefaultComputeHeuristic = AStarGrid2D.Heuristic.Octile;
        _navigation.DefaultEstimateHeuristic = AStarGrid2D.Heuristic.Octile;
        _navigation.Update();
        for (int y = 0; y < Game.World.Height; y++)
        for (int x = 0; x < Game.World.Width; x++)
            if (Game.World.IsBlocked(x, y)) _navigation.SetPointSolid(new Vector2I(x, y));
    }

    public Vector2 PathDirection(Vector2 from, Vector2 target)
    {
        var start = new Vector2I(Mathf.FloorToInt(from.X / WorldData.TileSize), Mathf.FloorToInt(from.Y / WorldData.TileSize));
        var end = new Vector2I(Mathf.FloorToInt(target.X / WorldData.TileSize), Mathf.FloorToInt(target.Y / WorldData.TileSize));
        if (!_navigation.IsInBoundsv(start) || !_navigation.IsInBoundsv(end) || _navigation.IsPointSolid(start) || _navigation.IsPointSolid(end))
            return Vector2.Zero;
        var path = _navigation.GetIdPath(start, end, true);
        if (path.Count < 2) return (target - from).Normalized();
        return (WorldData.Center(path[1]) - from).Normalized();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Game.IsPlaying) return;
        float dt = (float)delta;
        _time += dt;
        AttackCooldown = Mathf.Max(0, AttackCooldown - dt);
        DodgeEmpowerTime = Mathf.Max(0, DodgeEmpowerTime - dt);
        for (int i = 0; i < SkillCooldowns.Length; i++) SkillCooldowns[i] = Mathf.Max(0, SkillCooldowns[i] - dt);
        if (!Game.Wanderer.IsDead)
        {
            if (Input.IsActionJustPressed("charge_attack")) BeginCharge();
            if (_charging)
            {
                _chargeTime = Mathf.Min(.7f, _chargeTime + dt);
                if (!Input.IsActionPressed("charge_attack")) ReleaseCharge();
            }
            if (Input.IsActionPressed("attack")) TryAttack(Game.Wanderer.AimDirection);
            for (int i = 0; i < 3; i++)
                if (Input.IsActionJustPressed($"skill_{i + 1}")) TrySkill(i, Game.Wanderer.GetGlobalMousePosition());
        }
        UpdateAttack(dt);
        for (int i = _visuals.Count - 1; i >= 0; i--)
        {
            var visual = _visuals[i];
            visual.Age += dt;
            if (visual.Age < visual.Duration) continue;
            if (visual.Damage > 0 && !Game.Wanderer.IsDead && (visual.Source == null || visual.Source.IsAlive))
            {
                bool hit = visual.Line
                    ? DistanceToSegment(Game.Wanderer.GlobalPosition, visual.Position, visual.End) <= visual.Radius + 7
                    : Game.Wanderer.GlobalPosition.DistanceTo(visual.Position) <= visual.Radius + 7;
                if (hit && HasLineOfSight(visual.Position, Game.Wanderer.GlobalPosition)) DamagePlayer(visual.Damage, visual.Position);
                SpawnEffect(visual.Position, visual.Radius, new Color("f08462"), 0.3f);
            }
            _visuals.RemoveAt(i);
        }
        QueueRedraw();
    }

    public bool TryAttack(Vector2 direction)
    {
        if (!Game.IsPlaying || Game.Wanderer.IsDead || AttackCooldown > 0 || _charging) return false;
        if (direction.LengthSquared() < 0.01f) direction = Game.Wanderer.AimDirection;
        direction = direction.Normalized();
        WeaponKind kind = Game.State.WeaponId.Length == 0 ? WeaponKind.Sword : Items.Get(Game.State.WeaponId).Weapon;
        float cooldown = kind switch { WeaponKind.Sword => 0.37f, WeaponKind.Bow => 0.49f, _ => 0.74f };
        float windup = kind switch { WeaponKind.Sword => 0.08f, WeaponKind.Bow => 0.14f, _ => 0.23f };
        if (kind == WeaponKind.Sword)
        {
            _combo = _time - _lastSwordTime > 0.7f ? 0 : (_combo + 1) % 3;
            _lastSwordTime = _time;
            if (_combo == 2) cooldown += 0.08f;
        }
        AttackCooldown = cooldown * Game.Stats.CooldownScale;
        _attack = new Attack(kind, direction, windup, _combo);
        _attack.Damage = Game.Stats.Attack;
        _attack.WeaponId = Game.State.WeaponId;
        _attack.Type = Game.State.WeaponId.Length == 0 ? DamageKind.Slash : Items.Get(Game.State.WeaponId).Damage;
        _attack.Chill = ConsumeDodgeEmpower();
        Game.Record("attack", $"{Game.State.Area}:{Game.State.WeaponId};dir={direction.X:0.00},{direction.Y:0.00}");
        _swordHits.Clear();
        Game.Wanderer.BeginAttack(kind, direction, cooldown, _combo);
        return true;
    }

    private void UpdateAttack(float dt)
    {
        var attack = _attack;
        if (attack == null) return;
        if (Game.Wanderer.IsDead) { _attack = null; return; }
        attack.Time -= dt;
        if (attack.Time > 0 && !attack.Fired) return;
        var position = Game.Wanderer.GlobalPosition;
        int damage = attack.Damage;
        if (attack.Kind == WeaponKind.Sword)
        {
            float range = attack.Combo == 2 ? 78 : 68;
            if (!attack.Fired)
            {
                var color = attack.Combo == 2 ? new Color("efcc78") : new Color("c4efe5");
                _visuals.Add(new Visual { Position = position, Radius = range, Direction = attack.Direction, Cone = true, Duration = 0.16f, Color = color });
                attack.Fired = true;
                attack.Time = 0.09f;
            }
            foreach (var enemy in Enemies.ToArray())
            {
                if (!enemy.IsAlive || _swordHits.Contains(enemy)) continue;
                Vector2 offset = enemy.GlobalPosition - position;
                if (offset.Length() > range + (enemy.IsBoss ? 19 : 9) || (offset.Length() > 12 && attack.Direction.Dot(offset.Normalized()) < 0.25f)) continue;
                if (!HasLineOfSight(position, enemy.GlobalPosition)) continue;
                _swordHits.Add(enemy);
                enemy.TakeDamage((int)(damage * (attack.Combo == 2 ? 1.4f : 1f)), offset.Normalized() * (attack.Combo == 2 ? 170 : 100),
                    attack.Type, attack.WeaponId == "guardian_blade" && attack.Combo == 2, attack.Chill);
            }
            if (attack.Time <= 0) _attack = null;
        }
        else
        {
            bool magic = attack.Kind == WeaponKind.Staff;
            SpawnProjectile(position + attack.Direction * 19, attack.Direction, false,
                (int)(damage * (magic ? 1.15f : 1f)), magic ? 310 : 540, magic ? 6 : 3, magic,
                damageType: attack.Type, chill: attack.Chill);
            _attack = null;
        }
    }

    public bool BeginCharge()
    {
        if (!Game.IsPlaying || Game.Wanderer.IsDead || Game.State.WeaponId != "reed_bow" || AttackCooldown > 0 || _attack != null) return false;
        _charging = true;
        _chargeTime = 0;
        return true;
    }

    public bool ReleaseCharge()
    {
        if (!_charging) return false;
        float power = Mathf.Clamp(_chargeTime / .7f, 0, 1);
        _charging = false;
        Vector2 direction = Game.Wanderer.AimDirection;
        SpawnProjectile(Game.Wanderer.Position + direction * 19, direction, false,
            Mathf.RoundToInt(Game.Stats.Attack * (1 + .8f * power)), 620, 4, piercing: power >= .99f,
            damageType: DamageKind.Pierce, chill: ConsumeDodgeEmpower());
        AttackCooldown = .6f * Game.Stats.CooldownScale;
        Game.Wanderer.BeginAttack(WeaponKind.Bow, direction, .25f);
        Game.Record("charged_attack", $"power={power:0.00}");
        return true;
    }

    public void OnDodge()
    {
        if (Game.State.AccessoryId == "marsh_charm") DodgeEmpowerTime = 2;
        Game.Record("dodge", Game.State.Area);
    }

    private bool ConsumeDodgeEmpower()
    {
        bool result = DodgeEmpowerTime > 0;
        DodgeEmpowerTime = 0;
        return result;
    }

    public bool TrySkill(int index, Vector2 target)
    {
        string[] ids = { "cleave", "volley", "nova" };
        if (index < 0 || index >= ids.Length || !Game.IsPlaying || Game.Wanderer.IsDead || SkillCooldowns[index] > 0) return false;
        if (!Game.State.LearnedSkills.Contains(ids[index]))
        {
            Game.Notify("先在角色面板学习该技能。");
            return false;
        }
        Vector2 origin = Game.Wanderer.GlobalPosition;
        Vector2 direction = target - origin;
        if (direction.LengthSquared() < 0.1f) direction = Game.Wanderer.AimDirection;
        direction = direction.Normalized();
        int damage = Game.Stats.Attack;
        SkillCooldowns[index] = new[] { 6f, 8f, 10f }[index] * Game.Stats.CooldownScale;
        if (index == 0)
        {
            SpawnEffect(origin, 96, new Color("d9e4b3"), 0.38f);
            foreach (var enemy in Enemies.ToArray())
                if (enemy.IsAlive && enemy.GlobalPosition.DistanceTo(origin) <= 105 && HasLineOfSight(origin, enemy.GlobalPosition))
                    enemy.TakeDamage(damage * 2, (enemy.GlobalPosition - origin).Normalized() * 220, DamageKind.Slash);
            Game.Wanderer.BeginAttack(WeaponKind.Sword, direction, 0.35f);
        }
        else if (index == 1)
        {
            foreach (float angle in new[] { -0.16f, 0f, 0.16f })
                SpawnProjectile(origin + direction * 18, direction.Rotated(angle), false, (int)(damage * 1.4f), 560, 4, false, true);
            Game.Wanderer.BeginAttack(WeaponKind.Bow, direction, 0.25f);
        }
        else
        {
            target = origin + (target - origin).LimitLength(250);
            if (!HasLineOfSight(origin, target)) target = origin + direction * 65;
            SpawnEffect(target, 92, new Color("a9dbef"), 0.55f);
            foreach (var enemy in Enemies.ToArray())
                if (enemy.IsAlive && enemy.GlobalPosition.DistanceTo(target) <= 100 && HasLineOfSight(target, enemy.GlobalPosition))
                {
                    enemy.SlowTime = 3;
                    enemy.TakeDamage((int)(damage * 2.3f), (enemy.GlobalPosition - target).Normalized() * 100, DamageKind.Frost);
                }
            Game.Wanderer.BeginAttack(WeaponKind.Staff, direction, 0.35f);
        }
        Game.Puzzles.Record("skill_use", ids[index]);
        return true;
    }

    public bool DamagePlayer(int rawDamage, Vector2 source)
    {
        var player = Game.Wanderer;
        if (!Game.IsPlaying || player.IsDead || player.Invulnerable) return false;
        float damage = Mathf.Min(Game.State.Health, Mathf.Max(1, rawDamage - Game.Stats.Defense));
        Game.State.Health -= damage;
        player.HitFeedback();
        var direction = player.GlobalPosition - source;
        if (direction.LengthSquared() > 0.01f) player.Knockback(direction.Normalized() * 150);
        DamageNumber(player.GlobalPosition + new Vector2(0, -25), Mathf.CeilToInt(damage), new Color("f49b89"));
        Game.OnPlayerHit(Mathf.RoundToInt(damage));
        return true;
    }

    public void OnEnemyDeath(Enemy enemy) => Game.OnEnemyKilled(enemy);

    public void RemoveDead(Enemy enemy)
    {
        Enemies.Remove(enemy);
        enemy.QueueFree();
    }

    public void DropItem(string id, int count, Vector2 position)
    {
        Game.State.GroundLoot.Add(new LootState { Id = id, Count = count, Area = Game.World.Area, X = position.X, Y = position.Y });
        SpawnEffect(position, 24, new Color("e8ce86"), 0.5f);
    }

    public bool PickUpNearest()
    {
        var loot = NearestLoot();
        if (loot == null) return false;
        Items.Add(Game.State, loot.Id, loot.Count);
        Game.State.GroundLoot.Remove(loot);
        Game.Puzzles.Record("item_pickup", $"{loot.Id}:{loot.Count}");
        Game.Notify($"获得 {Items.Get(loot.Id).Name} ×{loot.Count}", true);
        return true;
    }

    private LootState? NearestLoot() => Game.State.GroundLoot.Where(loot => loot.Area == Game.World.Area && new Vector2(loot.X, loot.Y).DistanceSquaredTo(Game.Wanderer.GlobalPosition) < 50 * 50)
        .OrderBy(loot => new Vector2(loot.X, loot.Y).DistanceSquaredTo(Game.Wanderer.GlobalPosition)).FirstOrDefault();

    public Projectile SpawnProjectile(Vector2 position, Vector2 direction, bool enemy, int damage, float speed = 300,
        float radius = 4, bool magic = false, bool piercing = false, float lifetime = 2.5f, Enemy? source = null,
        DamageKind damageType = DamageKind.Pierce, bool chill = false)
    {
        var projectile = new Projectile { Position = position };
        _projectiles.Add(projectile);
        AddChild(projectile);
        projectile.Configure(this, direction, enemy, damage, speed, radius, magic, piercing, lifetime, source, damageType, chill);
        return projectile;
    }

    public void RemoveProjectile(Projectile projectile)
    {
        _projectiles.Remove(projectile);
        projectile.QueueFree();
    }

    public void CancelAttack()
    {
        _attack = null;
        _charging = false;
        _chargeTime = 0;
        _swordHits.Clear();
        Game.Wanderer.CancelAttackMotion();
    }

    public void ResetPlayerCombat()
    {
        CancelAttack();
        DodgeEmpowerTime = 0;
        _visuals.Clear();
        foreach (var projectile in _projectiles.ToArray()) projectile.Expire();
        AttackCooldown = 0;
        Array.Clear(SkillCooldowns);
        Game.Wanderer.ResetCombatMotion();
    }

    public bool HasLineOfSight(Vector2 from, Vector2 to)
    {
        var query = PhysicsRayQueryParameters2D.Create(from, to, 1);
        return GetWorld2D().DirectSpaceState.IntersectRay(query).Count == 0;
    }

    public void TelegraphCircle(Vector2 position, float radius, float duration, Color color, int damage = 0, Enemy? source = null)
        => _visuals.Add(new Visual { Position = position, Radius = radius, Duration = duration, Color = color, Damage = damage, Warning = true, Source = source });

    public void TelegraphLine(Vector2 from, Vector2 to, float width, float duration, int damage, Enemy? source = null)
        => _visuals.Add(new Visual { Position = from, End = to, Radius = width, Duration = duration, Color = new Color("ef9c62"), Damage = damage, Warning = true, Line = true, Source = source });

    public void SpawnEffect(Vector2 position, float radius, Color color, float duration = 0.4f)
        => _visuals.Add(new Visual { Position = position, Radius = radius, Duration = duration, Color = color });

    public void DamageNumber(Vector2 position, int number, Color color, string effect = "")
        => _visuals.Add(new Visual { Position = position, Duration = 0.65f, Color = color, Text = effect.Length == 0 ? number.ToString() : $"{number} {effect}" });

    public static float DistanceToSegment(Vector2 position, Vector2 from, Vector2 to)
    {
        Vector2 segment = to - from;
        float length = segment.LengthSquared();
        if (length < 0.01f) return position.DistanceTo(from);
        return position.DistanceTo(from + segment * Mathf.Clamp((position - from).Dot(segment) / length, 0, 1));
    }

    public override void _Draw()
    {
        Vector2 focus = Game.Wanderer.GlobalPosition;
        foreach (var loot in Game.State.GroundLoot)
        {
            Vector2 position = new(loot.X, loot.Y);
            if (loot.Area != Game.World.Area || position.DistanceSquaredTo(focus) > 800 * 800) continue;
            var color = Items.Get(loot.Id).Rarity switch
            {
                "史诗" => new Color("c4a0eb"), "稀有" => new Color("8bc8e0"), "精良" => new Color("b9d589"), _ => new Color("d7c591")
            };
            float bob = Mathf.Sin(_time * 3 + position.X) * 1.5f;
            DrawCircle(position, 12, new Color(color, 0.08f));
            DrawRect(new Rect2(position + new Vector2(-4, -8 + bob), new Vector2(8, 8)), color.Darkened(0.25f));
            DrawRect(new Rect2(position + new Vector2(-2, -10 + bob), new Vector2(4, 4)), color);
            DrawLine(position + new Vector2(-5, 4), position + new Vector2(5, 4), color, 1);
        }
        foreach (var visual in _visuals)
        {
            float t = visual.Age / visual.Duration;
            Color color = new(visual.Color, visual.Warning ? 0.45f + t * 0.4f : (1 - t) * 0.8f);
            if (visual.Text.Length > 0)
            {
                DrawString(_combatFont, visual.Position + new Vector2(-7, -t * 25), visual.Text, HorizontalAlignment.Left, -1, 17, color);
            }
            else if (visual.Line)
            {
                DrawLine(visual.Position, visual.End, new Color(color, 0.15f + t * 0.2f), visual.Radius * 2);
                DrawLine(visual.Position, visual.End, color, 2);
                DrawLine(visual.Position - (visual.End - visual.Position).Normalized().Orthogonal() * visual.Radius,
                    visual.End - (visual.End - visual.Position).Normalized().Orthogonal() * visual.Radius, color, 1);
                DrawLine(visual.Position + (visual.End - visual.Position).Normalized().Orthogonal() * visual.Radius,
                    visual.End + (visual.End - visual.Position).Normalized().Orthogonal() * visual.Radius, color, 1);
            }
            else if (visual.Cone)
            {
                float angle = visual.Direction.Angle();
                DrawArc(visual.Position, visual.Radius * (0.75f + 0.25f * t), angle - 1.2f, angle + 1.2f, 24, color, 5);
                DrawArc(visual.Position, visual.Radius * 0.7f, angle - 1.05f, angle + 1.05f, 20, new Color(color, color.A * 0.4f), 3);
            }
            else
            {
                float radius = visual.Warning ? visual.Radius : visual.Radius * (0.45f + t * 0.55f);
                DrawCircle(visual.Position, radius, new Color(color, color.A * 0.14f));
                DrawArc(visual.Position, radius, 0, Mathf.Tau, 40, color, visual.Warning ? 2 : 3);
                if (visual.Warning)
                {
                    DrawArc(visual.Position, radius * 0.82f, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * t, 32, color, 3);
                    DrawLine(visual.Position + new Vector2(-4, -4), visual.Position + new Vector2(4, 4), color, 1);
                    DrawLine(visual.Position + new Vector2(4, -4), visual.Position + new Vector2(-4, 4), color, 1);
                }
            }
        }
    }

    private sealed class Attack(WeaponKind kind, Vector2 direction, float windup, int combo)
    {
        public WeaponKind Kind = kind;
        public Vector2 Direction = direction;
        public float Time = windup;
        public int Combo = combo;
        public bool Fired;
        public int Damage;
        public string WeaponId = "";
        public DamageKind Type;
        public bool Chill;
    }

    private sealed class Visual
    {
        public Vector2 Position, End, Direction;
        public float Radius, Age, Duration;
        public Color Color;
        public bool Warning, Line, Cone;
        public int Damage;
        public string Text = "";
        public Enemy? Source;
    }
}
