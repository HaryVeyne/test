using Godot;
using System;
using System.Collections.Generic;

namespace ProjectUnknown;

public partial class Enemy : CharacterBody2D
{
    public string SpawnId = "";
    public string Kind = "scavenger";
    public int Level = 1;
    public int Health
    {
        get => _health;
        set { _health = value; _healthFraction = MaxHealth > 0 ? (float)value / MaxHealth : 1; }
    }
    private int _health;
    private float _healthFraction = 1;
    public int MaxHealth;
    public int ExperienceReward;
    public float SlowTime;
    public float ArmorBreakTime;
    public int Defense => ArmorBreakTime > 0 ? 0 : _defense;
    public int AttackDamage => _damage;
    public float MoveSpeed => _speed;
    public bool IsBoss => Kind is "root_boss" or "forge_boss";
    public bool IsAlive => Health > 0;
    public bool PhaseTwo => IsBoss && Health <= MaxHealth / 2;
    public bool IsAccessible => (!IsBoss || _combat.Game.Dungeons?.BossAccessible != false)
        && (SpawnId != "marsh_keeper" || _combat.Game.State.Activated.Contains("marsh_drained"));
    public string AiState => _mode.ToString();

    private enum Mode { Idle, Patrol, Alert, Chase, Windup, Attack, Recover, Return, Dead }
    private CombatController _combat = null!;
    private Mode _mode = Mode.Idle;
    private Vector2 _home;
    private Vector2 _facing = Vector2.Down;
    private Vector2 _attackDirection;
    private Vector2 _attackOrigin;
    private Vector2 _target;
    private Vector2 _steering;
    private Vector2 _knockback;
    private float _timer;
    private float _clock;
    private float _flash;
    private float _lostTime;
    private float _navigationTimer;
    private float _wanderAngle;
    private float _stride;
    private float _deathTime;
    private int _attackNumber;
    private int _selectedAttack;
    private bool _dashHit;
    private bool _announcedPhase;
    private int _damage;
    private float _speed;
    private float _attackRange;
    private int _baseHealth, _baseDamage, _baseDefense, _defense;
    private float _baseSpeed;

    public void Configure(CombatController combat, EnemySpawn spawn)
    {
        _combat = combat;
        SpawnId = spawn.Id;
        Kind = spawn.Kind;
        Level = spawn.Level;
        Position = _home = WorldData.Center(spawn.Tile);
        int variation = 0;
        foreach (char c in SpawnId) variation = (variation * 31 + c) & 0xffff;
        _wanderAngle = variation * 0.017f;
        _timer = 0.6f + variation % 9 * 0.12f;
        (int hp, int damage, float speed, float range, int xp) = Kind switch
        {
            "archer" => (42, 13, 82f, 275f, 24),
            "wolf" => (38, 14, 136f, 150f, 23),
            "guardian" => (100, 24, 51f, 59f, 38),
            "spore" => (52, 16, 61f, 245f, 29),
            "elite" => (175, 24, 93f, 130f, 75),
            "root_boss" => (430, 27, 61f, 195f, 220),
            "forge_boss" => (650, 32, 54f, 390f, 340),
            _ => (40, 11, 89f, 40f, 19)
        };
        _baseHealth = IsBoss ? hp : hp + (Level - 1) * 12;
        _baseDamage = damage + (Level - 1) * (IsBoss ? 1 : 2);
        _baseDefense = Kind == "guardian" ? 4 + Level : Kind == "elite" ? 3 : 0;
        _baseSpeed = speed;
        ApplyDifficulty();
        _attackRange = range;
        ExperienceReward = xp + (Level - 1) * 6;
    }

    public void ApplyDifficulty()
    {
        var profile = CombatRules.Profile(_combat.Game.State.Difficulty);
        MaxHealth = Mathf.CeilToInt(_baseHealth * profile.Health);
        _health = _healthFraction > 0 ? Math.Max(1, Mathf.RoundToInt(MaxHealth * _healthFraction)) : 0;
        _damage = Mathf.CeilToInt(_baseDamage * profile.Attack);
        _defense = Math.Max(0, _baseDefense + profile.Defense);
        _speed = _baseSpeed * profile.Speed;
    }

    public override void _Ready()
    {
        CollisionLayer = 4;
        CollisionMask = 5;
        ZIndex = 19;
        using var shape = new CircleShape2D { Radius = IsBoss ? 19 : Kind == "guardian" ? 11 : 8 };
        AddChild(new CollisionShape2D
        {
            Shape = shape,
            Position = new Vector2(0, 3)
        });
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_combat.Game.IsPlaying) return;
        float dt = (float)delta;
        if (!IsAlive)
        {
            _deathTime += dt;
            if (_deathTime >= 0.45f) { _combat.RemoveDead(this); return; }
            Modulate = new Color(1, 1, 1, Mathf.Max(0, 1 - _deathTime / 0.45f));
            Scale = Vector2.One * (1 + _deathTime * 0.3f);
            QueueRedraw();
            return;
        }
        if (!IsAccessible)
        {
            Velocity = Vector2.Zero;
            _mode = Mode.Idle;
            return;
        }
        Vector2 player = _combat.Game.Wanderer.Position;
        float distance = Position.DistanceTo(player);
        if (distance > 700 && _mode is not (Mode.Windup or Mode.Attack or Mode.Return)) return;
        _clock += dt;
        _timer -= dt;
        _flash = Mathf.Max(0, _flash - dt);
        SlowTime = Mathf.Max(0, SlowTime - dt);
        ArmorBreakTime = Mathf.Max(0, ArmorBreakTime - dt);
        _navigationTimer -= dt;
        _knockback = _knockback.MoveToward(Vector2.Zero, dt * 780);
        Velocity = _knockback;
        float speed = _speed * (SlowTime > 0 ? 0.55f : 1) * (PhaseTwo ? 1.14f : 1);
        if (PhaseTwo && !_announcedPhase)
        {
            _announcedPhase = true;
            _combat.SpawnEffect(Position, 68, Kind == "root_boss" ? new Color("aad980") : new Color("ffad64"), 0.75f);
            _combat.Game.Notify(Kind == "root_boss" ? "古木守卫：藤蔓加速" : "熔炉核心：过载", true);
        }
        switch (_mode)
        {
            case Mode.Idle:
                if (distance < DetectionRange() && ClearSight(player)) Alert(player);
                else if (_timer <= 0 && !IsBoss)
                {
                    _mode = Mode.Patrol;
                    _target = _home + Vector2.FromAngle(_wanderAngle + _clock * 0.3f) * 38;
                    _timer = 1.8f;
                }
                break;
            case Mode.Patrol:
                if (distance < DetectionRange() && ClearSight(player)) Alert(player);
                else
                {
                    Navigate(_target, speed * 0.34f);
                    if (_timer <= 0 || Position.DistanceTo(_target) < 9) Rest(Mode.Idle, 1.8f);
                }
                break;
            case Mode.Alert:
                _facing = Position.DirectionTo(player);
                if (_timer <= 0) _mode = Mode.Chase;
                break;
            case Mode.Chase:
                if (distance > DetectionRange() + 150 || Position.DistanceTo(_home) > (IsBoss ? 265 : 450))
                {
                    _lostTime += dt;
                    if (_lostTime > 1.5f) { _mode = Mode.Return; _navigationTimer = 0; }
                }
                else _lostTime = 0;
                _facing = Position.DirectionTo(player);
                bool rangeReady = distance <= _attackRange && ClearSight(player);
                if (rangeReady && _timer <= 0) BeginAttack(player);
                else if (Kind is "archer" or "spore" && distance < 115)
                    Navigate(Position + (Position - player).Normalized() * 64, speed * 0.65f);
                else if (!rangeReady || distance > _attackRange * 0.7f) Navigate(player, speed);
                break;
            case Mode.Windup:
                if (_timer <= 0) ResolveAttack();
                break;
            case Mode.Attack:
                Velocity += _attackDirection * (Kind == "wolf" ? 360 : 330) * CombatRules.Profile(_combat.Game.State.Difficulty).Speed * (SlowTime > 0 ? 0.65f : 1);
                if (!_dashHit && distance < (IsBoss ? 35 : 25))
                    _dashHit = _combat.DamagePlayer(_damage, Position);
                if (_timer <= 0 || IsOnWall()) Rest(Mode.Recover, 0.75f);
                break;
            case Mode.Recover:
                if (_timer <= 0) { _mode = Mode.Chase; _timer = PhaseTwo ? 0.25f : 0.4f; }
                break;
            case Mode.Return:
                Navigate(_home, speed * 0.85f);
                if (Position.DistanceTo(_home) < 14) Rest(Mode.Idle, 1);
                else if (distance < 130 && Position.DistanceTo(_home) < (IsBoss ? 200 : 320)) Alert(player);
                break;
        }
        Vector2 previous = Position;
        MoveAndSlide();
        _stride += previous.DistanceTo(Position) * 0.13f;
        QueueRedraw();
    }

    private float DetectionRange() => IsBoss ? 420 : Kind is "archer" or "elite" ? 330 : 260;

    private void Alert(Vector2 player)
    {
        _mode = Mode.Alert;
        _timer = 0.35f;
        _lostTime = 0;
        _facing = Position.DirectionTo(player);
    }

    private void Rest(Mode mode, float duration)
    {
        _mode = mode;
        _timer = duration;
        Velocity = _knockback;
    }

    private void Navigate(Vector2 target, float speed)
    {
        if (_navigationTimer <= 0)
        {
            _steering = _combat.PathDirection(Position, target);
            _navigationTimer = 0.18f;
        }
        Velocity += _steering * speed;
        if (_steering.LengthSquared() > 0.1f) _facing = _steering;
    }

    private bool ClearSight(Vector2 target)
    {
        Vector2 displacement = target - Position;
        int steps = Math.Max(1, (int)(displacement.Length() / 16));
        var world = _combat.Game.World;
        for (int i = 1; i < steps; i++)
        {
            Vector2 at = Position + displacement * i / steps;
            if (world.IsBlocked((int)(at.X / WorldData.TileSize), (int)(at.Y / WorldData.TileSize))) return false;
        }
        return true;
    }

    private void BeginAttack(Vector2 player)
    {
        _mode = Mode.Windup;
        _attackOrigin = Position;
        _attackDirection = Position.DirectionTo(player);
        _facing = _attackDirection;
        _target = player;
        _selectedAttack = _attackNumber++ % (IsBoss || Kind == "elite" ? 3 : 1);
        Color warning = new("ef9b76");
        if (Kind == "root_boss")
        {
            _timer = PhaseTwo ? 0.7f : 0.95f;
            if (_selectedAttack == 1)
            {
                _combat.TelegraphCircle(Position, 76, _timer, warning, _damage, source: this);
                int count = PhaseTwo ? 8 : 6;
                for (int i = 0; i < count; i++)
                    _combat.TelegraphCircle(Position + Vector2.FromAngle(i * Mathf.Tau / count) * 140,
                        37, _timer + 0.28f, warning, _damage, source: this);
            }
        }
        else if (Kind == "forge_boss")
        {
            _timer = PhaseTwo ? 0.75f : 1f;
            if (_selectedAttack == 0)
                _combat.TelegraphLine(Position, Position + _attackDirection * 530, 42, _timer, _damage + 4, source: this);
            else if (_selectedAttack == 1)
            {
                _combat.TelegraphCircle(player, 58, _timer, warning, _damage, source: this);
                Vector2 side = _attackDirection.Orthogonal() * 95;
                _combat.TelegraphCircle(player + side, 48, _timer + 0.22f, warning, _damage, source: this);
                _combat.TelegraphCircle(player - side, 48, _timer + 0.44f, warning, _damage, source: this);
                if (PhaseTwo) _combat.TelegraphCircle(player - _attackDirection * 95, 42, _timer + 0.66f, warning, _damage, source: this);
            }
        }
        else if (Kind == "archer")
        {
            _timer = 0.65f;
            _combat.TelegraphLine(Position, player + _attackDirection * 30, 5, _timer, 0, source: this);
        }
        else if (Kind == "wolf" || Kind == "elite" && _selectedAttack == 2)
        {
            _timer = Kind == "wolf" ? 0.52f : 0.68f;
            _combat.TelegraphLine(Position, Position + _attackDirection * 210, 24, _timer, 0, source: this);
        }
        else if (Kind == "spore")
        {
            _timer = 0.95f;
            _combat.TelegraphCircle(player, 58, _timer, new Color("c7b871"), _damage, source: this);
        }
        else if (Kind == "elite" && _selectedAttack == 1)
        {
            _timer = 0.7f;
            _combat.TelegraphLine(Position, Position + _attackDirection * 240, 18, _timer, 0, source: this);
        }
        else
        {
            _timer = Kind == "guardian" ? 0.8f : Kind == "elite" ? 0.65f : 0.42f;
            _combat.TelegraphCircle(Position + _attackDirection * 14, Kind == "guardian" ? 61 : Kind == "elite" ? 58 : 33,
                _timer, warning, _damage, source: this);
        }
    }

    private void ResolveAttack()
    {
        if (Kind == "root_boss")
        {
            if (_selectedAttack == 0)
            {
                Vector2 offset = _combat.Game.Wanderer.Position - _attackOrigin;
                if (offset.Length() <= 168 && (offset.Length() < 25 || offset.Normalized().Dot(_attackDirection) >= MathF.Cos(1.18f)))
                    _combat.DamagePlayer(_damage, _attackOrigin);
                _combat.SpawnEffect(_attackOrigin + _attackDirection * 80, 75, new Color("a5c783"), 0.35f);
            }
            else if (_selectedAttack == 2) FireFan(PhaseTwo ? 7 : 5, 0.24f, 230, true);
            Rest(Mode.Recover, PhaseTwo ? 0.6f : 1.05f);
        }
        else if (Kind == "forge_boss")
        {
            if (_selectedAttack == 2)
            {
                int count = PhaseTwo ? 14 : 10;
                float rotate = PhaseTwo ? 0.15f : 0;
                for (int i = 0; i < count; i++)
                    _combat.SpawnProjectile(Position, Vector2.FromAngle(Mathf.Tau * i / count + rotate), true,
                        _damage, PhaseTwo ? 240 : 195, 7, true, false, 3, source: this);
            }
            Rest(Mode.Recover, PhaseTwo ? 0.6f : 1f);
        }
        else if (Kind == "archer")
        {
            _combat.SpawnProjectile(Position + _attackDirection * 15, _attackDirection, true, _damage, 315, 4, source: this);
            Rest(Mode.Recover, 0.9f);
        }
        else if (Kind == "wolf" || Kind == "elite" && _selectedAttack == 2)
        {
            _dashHit = false;
            _mode = Mode.Attack;
            _timer = Kind == "wolf" ? 0.48f : 0.6f;
        }
        else if (Kind == "elite" && _selectedAttack == 1)
        {
            FireFan(3, 0.18f, 265, false);
            Rest(Mode.Recover, 0.85f);
        }
        else
        {
            _combat.SpawnEffect(Kind == "spore" ? _target : Position + _attackDirection * 17,
                Kind == "guardian" ? 57 : Kind == "spore" ? 53 : 30,
                Kind == "spore" ? new Color("a8bd6e") : new Color("d9a27b"));
            Rest(Mode.Recover, Kind == "guardian" ? 1.2f : Kind == "spore" ? 1.3f : 0.68f);
        }
    }

    private void FireFan(int count, float spread, float speed, bool magic)
    {
        for (int i = 0; i < count; i++)
            _combat.SpawnProjectile(Position + _attackDirection * 20,
                _attackDirection.Rotated((i - (count - 1) / 2f) * spread), true,
                _damage, speed, magic ? 7 : 4, magic, false, 3, source: this);
    }

    public void TakeDamage(int damage, Vector2 knockback, DamageKind type = DamageKind.Environment,
        bool breakArmor = false, bool chill = false)
    {
        if (!_combat.Game.IsPlaying || !IsAlive || !IsAccessible) return;
        if (_mode is Mode.Idle or Mode.Patrol or Mode.Return) Alert(_combat.Game.Wanderer.Position);
        if (breakArmor) ArmorBreakTime = 3;
        if (chill) SlowTime = Mathf.Max(SlowTime, 2);
        float multiplier = CombatRules.Multiplier(Kind, type);
        int actual = Math.Min(Health, Math.Max(1, Mathf.RoundToInt(damage * multiplier) - Defense));
        Health = Math.Max(0, Health - actual);
        _combat.Game.OnEnemyHit(actual);
        _flash = 0.16f;
        _knockback += knockback * (IsBoss ? 0.1f : Kind == "guardian" ? 0.35f : 1);
        _combat.Game.State.DamageDealt += actual;
        string effect = breakArmor ? "破甲" : multiplier > 1 ? "弱点" : multiplier < 1 ? "抵抗" : chill ? "减速" : "";
        _combat.DamageNumber(Position + new Vector2(0, -30), actual,
            multiplier < 1 ? new Color("9dafb9") : new Color("ead89d"), effect);
        _combat.SpawnEffect(Position + new Vector2(0, -7), IsBoss ? 25 : 14, new Color("eed5a0"), 0.12f);
        if (Health == 0)
        {
            _mode = Mode.Dead;
            CollisionLayer = 0;
            Velocity = Vector2.Zero;
            _combat.OnEnemyDeath(this);
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        float bob = Mathf.Sin(_stride) * (IsBoss ? 0.7f : 1.1f);
        Color ink = new("253a40");
        Color body = Kind switch
        {
            "archer" => new Color("918964"),
            "wolf" => new Color("bac6b6"),
            "guardian" => new Color("81908a"),
            "spore" => new Color("b4a269"),
            "elite" => new Color("ab7180"),
            "root_boss" => new Color("89925b"),
            "forge_boss" => new Color("9ba6a8"),
            _ => new Color("ad8674")
        };
        if (SpawnId == "marsh_keeper") body = new Color("68a5a0");
        if (_flash > 0) body = body.Lerp(new Color("fff0cd"), 0.85f);
        float shadow = IsBoss ? 31 : Kind == "guardian" ? 16 : 12;
        DrawEllipse(new Vector2(0, 9), new Vector2(shadow, shadow * 0.35f), new Color(0.02f, 0.04f, 0.05f, 0.4f));
        if (SlowTime > 0) DrawArc(Vector2.Zero, shadow + 3, 0, Mathf.Tau, 24, new Color("87c7d6"), 2);
        if (ArmorBreakTime > 0) DrawArc(Vector2.Zero, shadow + 6, 0, Mathf.Tau, 16, new Color("e8ad71"), 2);
        switch (Kind)
        {
            case "wolf":
                DrawWolf(body, ink, bob);
                break;
            case "spore":
                Rect(-7, -5 + bob, 14, 14, ink);
                Rect(-5, -5 + bob, 10, 13, new Color("d3c8a3"));
                DrawCircle(new Vector2(0, -13 + bob), 13, body);
                Rect(-15, -11 + bob, 30, 4, body.Darkened(0.25f));
                Rect(-9, -18 + bob, 5, 4, new Color("e4d7a7"));
                Rect(4, -14 + bob, 5, 3, new Color("e4d7a7"));
                Rect(-3, -3 + bob, 2, 3, ink); Rect(2, -3 + bob, 2, 3, ink);
                break;
            case "root_boss":
                DrawRootGuardian(body, ink, bob);
                break;
            case "forge_boss":
                DrawForgeCore(body, ink, bob);
                break;
            default:
                DrawHumanoid(body, ink, bob);
                break;
        }
        if (_mode == Mode.Alert)
        {
            float height = IsBoss ? 68 : 30;
            Rect(-1, -height, 3, 8, new Color("efd59a"));
            Rect(-1, -height + 11, 3, 3, new Color("efd59a"));
        }
        if (IsAlive && !IsBoss && Health < MaxHealth)
        {
            Rect(-17, -32, 34, 4, ink);
            Rect(-16, -31, 32f * Health / MaxHealth, 2, new Color("d79c89"));
        }
        if (_mode == Mode.Windup)
        {
            float pulse = 0.55f + Mathf.Sin(_clock * 21) * 0.15f;
            Color warning = new(1, 0.5f, 0.34f, pulse);
            DrawArc(Vector2.Zero, shadow + 6, 0, Mathf.Tau, 32, warning, 2);
            if (Kind == "root_boss" && _selectedAttack == 0)
            {
                var vertices = new List<Vector2> { Vector2.Zero };
                for (int i = 0; i <= 24; i++) vertices.Add(_attackDirection.Rotated(-1.18f + i * 2.36f / 24) * 168);
                DrawColoredPolygon(vertices.ToArray(), new Color(0.85f, 0.5f, 0.3f, 0.18f));
                DrawArc(Vector2.Zero, 168, _attackDirection.Angle() - 1.18f, _attackDirection.Angle() + 1.18f, 30, warning, 3);
                DrawLine(Vector2.Zero, _attackDirection.Rotated(-1.18f) * 168, warning, 2);
                DrawLine(Vector2.Zero, _attackDirection.Rotated(1.18f) * 168, warning, 2);
            }
            else if (Kind == "forge_boss" && _selectedAttack == 2 || Kind == "root_boss" && _selectedAttack == 2)
            {
                int rays = Kind == "forge_boss" ? 10 : 5;
                for (int i = 0; i < rays; i++)
                {
                    Vector2 direction = Kind == "forge_boss" ? Vector2.FromAngle(i * Mathf.Tau / rays) : _attackDirection.Rotated((i - 2) * 0.24f);
                    DrawLine(direction * 32, direction * 82, warning, 2);
                }
            }
        }
    }

    private void DrawHumanoid(Color body, Color ink, float bob)
    {
        float step = Mathf.Sin(_stride) * 2;
        Rect(-6, 1 + step, 5, 9, ink); Rect(2, 1 - step, 5, 9, ink);
        Rect(-9, -9 + bob, 18, 14, ink);
        Rect(-7, -10 + bob, 14, 13, body);
        Rect(-5, -19 + bob, 11, 9, body.Lightened(0.13f));
        Rect(-5, -19 + bob, 11, 4, ink);
        Vector2 eyes = _facing * 2;
        Rect(eyes.X - 3, -13 + eyes.Y + bob, 2, 2, new Color("f0d28c"));
        Rect(eyes.X + 2, -13 + eyes.Y + bob, 2, 2, new Color("f0d28c"));
        if (Kind == "archer")
        {
            Vector2 at = _facing * 13 + new Vector2(0, -2);
            DrawArc(at, 12, _facing.Angle() - 1.1f, _facing.Angle() + 1.1f, 12, new Color("dec693"), 2);
            DrawLine(at + _facing.Rotated(-1.1f) * 12, at + _facing.Rotated(1.1f) * 12, ink, 1);
        }
        else if (Kind == "guardian")
        {
            Rect(-12, -8 + bob, 8, 17, body.Darkened(0.23f));
            Rect(-11, -7 + bob, 2, 14, new Color("bbc1ab"));
            Rect(7, -16 + bob, 7, 22, ink);
            Rect(5, -20 + bob, 12, 9, new Color("bec2a8"));
            Rect(-6, -20 + bob, 12, 6, new Color("b4b9aa"));
        }
        else
        {
            Vector2 blade = _facing * 12 + new Vector2(0, -3);
            DrawLine(blade, blade + _facing * (Kind == "elite" ? 21 : 13), new Color("d7d4bd"), Kind == "elite" ? 4 : 3);
            if (Kind == "elite")
            {
                Rect(-10, -15 + bob, 4, 8, new Color("d8ba85"));
                Rect(7, -15 + bob, 4, 8, new Color("d8ba85"));
                Rect(-3, -21 + bob, 6, 4, new Color("d8ba85"));
            }
        }
    }

    private void DrawWolf(Color body, Color ink, float bob)
    {
        float facing = _facing.X < 0 ? -1 : 1;
        Rect(-12, -7 + bob, 24, 14, ink);
        Rect(-12, -8 + bob, 23, 12, body);
        Rect(-11, 3, 4, 7 + Mathf.Sin(_stride) * 2, ink);
        Rect(7, 3, 4, 7 - Mathf.Sin(_stride) * 2, ink);
        Rect(facing * 9 - 6, -15 + bob, 12, 12, body);
        Rect(facing * 16 - 4, -6 + bob, 8, 5, body.Darkened(0.2f));
        Vector2[] ear = { new(facing * 8 - 5, -13 + bob), new(facing * 8 - 3, -22 + bob), new(facing * 8 + 2, -12 + bob) };
        DrawColoredPolygon(ear, body.Darkened(0.16f));
        Rect(facing * 12, -11 + bob, 3, 2, new Color("dfb578"));
        DrawLine(new Vector2(-facing * 11, -3), new Vector2(-facing * 21, -10 + bob), body, 4);
    }

    private void DrawRootGuardian(Color body, Color ink, float bob)
    {
        DrawLine(new Vector2(-10, 7), new Vector2(-26, 18), ink, 7);
        DrawLine(new Vector2(10, 7), new Vector2(27, 17), ink, 7);
        Rect(-24, -31 + bob, 48, 42, ink);
        Rect(-20, -35 + bob, 40, 44, body.Darkened(0.16f));
        Rect(-14, -39 + bob, 10, 47, body);
        Rect(5, -35 + bob, 8, 39, body.Lightened(0.15f));
        DrawLine(new Vector2(-20, -18), new Vector2(-36, -36 + bob), body, 9);
        DrawLine(new Vector2(-36, -36 + bob), new Vector2(-46, -28 + bob), body, 7);
        DrawLine(new Vector2(21, -20), new Vector2(38, -38 + bob), body, 10);
        DrawLine(new Vector2(38, -38 + bob), new Vector2(53, -28 + bob), body, 6);
        Rect(-13, -48 + bob, 27, 19, ink);
        Rect(-11, -50 + bob, 23, 16, body);
        Color glow = PhaseTwo ? new Color("f0c67b") : new Color("c8e8a5");
        Rect(-9, -42 + bob, 6, 4, glow); Rect(4, -42 + bob, 6, 4, glow);
        Rect(-4, -20 + bob, 9, 12, glow);
        DrawLine(new Vector2(-7, -47), new Vector2(-19, -64 + bob), body.Darkened(0.2f), 5);
        DrawLine(new Vector2(7, -47), new Vector2(20, -63 + bob), body.Darkened(0.2f), 5);
        DrawCircle(new Vector2(-22, -56 + bob), 10, new Color("749363"));
        DrawCircle(new Vector2(24, -55 + bob), 11, new Color("688959"));
    }

    private void DrawForgeCore(Color body, Color ink, float bob)
    {
        Rect(-27, 3, 13, 12, ink); Rect(14, 3, 13, 12, ink);
        Rect(-34, -28 + bob, 17, 34, ink); Rect(17, -28 + bob, 17, 34, ink);
        Rect(-32, -27 + bob, 12, 29, body.Darkened(0.3f));
        Rect(20, -27 + bob, 12, 29, body.Darkened(0.3f));
        Rect(-21, -39 + bob, 42, 49, ink);
        Rect(-18, -39 + bob, 36, 44, body);
        Rect(-13, -34 + bob, 26, 31, body.Darkened(0.3f));
        Color glow = PhaseTwo ? new Color("ffe090") : new Color("f5ab68");
        DrawCircle(new Vector2(0, -20 + bob), 10, ink);
        DrawCircle(new Vector2(0, -20 + bob), 7 + Mathf.Sin(_clock * 4) * 0.6f, glow);
        DrawLine(new Vector2(-15, -5), new Vector2(15, -5), new Color("d9b778"), 3);
        Rect(-14, -48 + bob, 28, 10, ink);
        Rect(-11, -47 + bob, 22, 7, body);
        Rect(-9, -44 + bob, 18, 3, glow);
        Rect(-29, -35 + bob, 5, 10, new Color("b4bdba"));
        Rect(24, -35 + bob, 5, 10, new Color("b4bdba"));
        DrawLine(new Vector2(-24, -30), new Vector2(-34, -46), new Color("a0aaa6"), 6);
        DrawLine(new Vector2(24, -30), new Vector2(34, -46), new Color("a0aaa6"), 6);
        if (PhaseTwo)
        {
            DrawCircle(new Vector2(-34, -51 + bob), 5, new Color(glow, 0.7f));
            DrawCircle(new Vector2(34, -51 + bob), 5, new Color(glow, 0.7f));
        }
    }

    private void Rect(float x, float y, float w, float h, Color color) => DrawRect(new Rect2(x, y, w, h), color);

    private void DrawEllipse(Vector2 center, Vector2 size, Color color)
    {
        var vertices = new Vector2[20];
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = center + Vector2.FromAngle(i * Mathf.Tau / vertices.Length) * size;
        DrawColoredPolygon(vertices, color);
    }
}
