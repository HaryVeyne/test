using Godot;
using System;

namespace ProjectUnknown;

public partial class Player : CharacterBody2D
{
    public bool Active { get; set; }
    public event Action<float, bool>? Travelled;
    public Vector2 AimDirection { get; private set; } = Vector2.Down;
    public bool Invulnerable => _invulnerability > 0;
    public bool IsDead => _game != null && _game.State.Health <= 0;
    public float DodgeCooldown { get; private set; }
    public float AttackMotion { get; private set; }

    private Game _game = null!;
    private bool _wasMoving;
    private float _stride;
    private float _dodgeTime;
    private float _invulnerability;
    private float _flash;
    private float _attackDuration;
    private Vector2 _dodgeDirection;
    private Vector2 _knockback;
    private Vector2 _attackDirection = Vector2.Down;
    private WeaponKind _attackWeapon;
    private int _combo;

    public void Configure(Game game) => _game = game;

    public override void _Ready()
    {
        CollisionLayer = 2;
        CollisionMask = 1;
        AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 7 }, Position = new Vector2(0, 3) });
        ZIndex = 20;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Active || !_game.IsPlaying || IsDead)
        {
            Velocity = Vector2.Zero;
            _wasMoving = false;
            QueueRedraw();
            return;
        }
        float dt = (float)delta;
        DodgeCooldown = Mathf.Max(0, DodgeCooldown - dt);
        _invulnerability = Mathf.Max(0, _invulnerability - dt);
        _flash = Mathf.Max(0, _flash - dt);
        AttackMotion = Mathf.Max(0, AttackMotion - dt);
        var mouseDirection = GetGlobalMousePosition() - GlobalPosition;
        if (mouseDirection.LengthSquared() > 9) AimDirection = mouseDirection.Normalized();
        if (Input.IsActionJustPressed("dodge")) TryDodge();
        var direction = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        float speed = _game.Stats.MoveSpeed * (Input.IsActionPressed("run") ? 1.38f : 1f);
        if (AttackMotion > 0) speed *= 0.78f;
        if (_dodgeTime > 0)
        {
            _dodgeTime -= dt;
            Velocity = _dodgeDirection * 520;
        }
        else
        {
            _knockback = _knockback.MoveToward(Vector2.Zero, dt * 800);
            Velocity = Velocity.MoveToward(direction * speed + _knockback, dt * 1200);
        }
        var before = Position;
        MoveAndSlide();
        float distance = before.DistanceTo(Position);
        bool moving = distance > 0.03f;
        if (moving)
        {
            _stride += distance * 0.095f;
            Travelled?.Invoke(distance, !_wasMoving);
        }
        _wasMoving = moving;
        QueueRedraw();
    }

    public bool TryDodge()
    {
        if (!Active || !_game.IsPlaying || IsDead || DodgeCooldown > 0) return false;
        _dodgeDirection = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        if (_dodgeDirection == Vector2.Zero) _dodgeDirection = AimDirection;
        _dodgeTime = 0.20f;
        _invulnerability = 0.32f;
        DodgeCooldown = 1.05f;
        _game.Combat.CancelAttack();
        _game.Combat.OnDodge();
        _game.Combat.SpawnEffect(GlobalPosition, 20, new Color("9ce2dc"), 0.25f);
        return true;
    }

    public void Knockback(Vector2 force) => _knockback += force;

    public void HitFeedback()
    {
        _flash = 0.18f;
        _invulnerability = 0.55f;
    }

    public void BeginAttack(WeaponKind kind, Vector2 direction, float duration, int combo = 0)
    {
        _attackWeapon = kind;
        _attackDirection = direction;
        _attackDuration = duration;
        AttackMotion = duration;
        _combo = combo;
    }

    public void CancelAttackMotion() => AttackMotion = 0;

    public void ResetCombatMotion()
    {
        _knockback = Vector2.Zero;
        _invulnerability = 1f;
        _dodgeTime = 0;
        _flash = 0;
        DodgeCooldown = 0;
        AttackMotion = 0;
    }

    public override void _Draw()
    {
        float bob = Velocity.LengthSquared() > 10 ? Mathf.Sin(_stride) * 1.3f : 0;
        DrawRect(new Rect2(-9, 7, 18, 5), new Color(0.02f, 0.06f, 0.06f, 0.35f));
        var coat = _flash > 0 ? new Color("ffffff") : new Color("d4ba87");
        var dark = new Color("283944");
        float step = Mathf.Sin(_stride) * 2;
        DrawRect(new Rect2(-5, 3 + step, 4, 7), dark);
        DrawRect(new Rect2(2, 3 - step, 4, 7), dark);
        DrawRect(new Rect2(-7, -6 + bob, 14, 12), dark);
        DrawRect(new Rect2(-6, -7 + bob, 12, 11), coat);
        DrawRect(new Rect2(-3, -6 + bob, 3, 11), new Color("ebd8ab"));
        DrawRect(new Rect2(-5, -14 + bob, 10, 8), new Color("b99272"));
        DrawRect(new Rect2(-6, -16 + bob, 12, 5), dark);
        DrawRect(new Rect2(-6, -12 + bob, 3, 3), dark);
        DrawRect(new Rect2(-9, -7 + bob, 3, 9), coat.Darkened(0.18f));
        DrawRect(new Rect2(7, -7 + bob, 3, 9), coat.Darkened(0.18f));
        if (_invulnerability > 0)
            DrawArc(Vector2.Zero, 15, 0, Mathf.Tau, 24, new Color(0.65f, 0.94f, 0.92f, 0.75f), 1.5f);
        if (_game == null) return;
        var kind = AttackMotion > 0 ? _attackWeapon : (_game.State.WeaponId.Length == 0 ? WeaponKind.Sword : Items.Get(_game.State.WeaponId).Weapon);
        Vector2 direction = AttackMotion > 0 ? _attackDirection : AimDirection;
        float attackPhase = AttackMotion > 0 ? 1 - AttackMotion / _attackDuration : 0;
        Vector2 hand = new(0, -3 + bob);
        if (kind == WeaponKind.Sword)
        {
            float turn = AttackMotion > 0 ? Mathf.Lerp(-1.3f, 1.3f, attackPhase) * (_combo % 2 == 0 ? 1 : -1) : 0.1f;
            Vector2 bladeDirection = direction.Rotated(turn);
            Vector2 grip = hand + bladeDirection * 10;
            DrawLine(grip, grip + bladeDirection * 22, new Color("d3e7e3"), 5);
            DrawLine(grip + bladeDirection * 2 - bladeDirection.Orthogonal() * 5,
                grip + bladeDirection * 2 + bladeDirection.Orthogonal() * 5, new Color("a99063"), 3);
            DrawLine(grip, grip - bladeDirection * 6, new Color("665343"), 4);
        }
        else if (kind == WeaponKind.Bow)
        {
            Vector2 bow = hand + direction * 15;
            DrawArc(bow, 12, direction.Angle() - Mathf.Pi / 2, direction.Angle() + Mathf.Pi / 2, 12, new Color("b99869"), 3);
            DrawLine(bow + direction.Orthogonal() * 12, bow - direction.Orthogonal() * 12, new Color("e2d6b6"), 1);
            if (AttackMotion > 0) DrawLine(hand, hand + direction * 26, new Color("f2e4ac"), 2);
            if (_game.Combat.ChargeProgress > 0)
            {
                DrawArc(hand, 24, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * _game.Combat.ChargeProgress, 24, new Color("edcb78"), 3);
                DrawLine(hand + direction * 30, hand + direction * 64, new Color("edcb78"), 1);
            }
        }
        else
        {
            Vector2 grip = hand + direction * 10;
            DrawLine(grip - direction * 7, grip + direction * 18, new Color("907a66"), 4);
            DrawCircle(grip + direction * 18, 5, new Color("b1ded4"));
            DrawCircle(grip + direction * 18, AttackMotion > 0 ? 10 : 7, new Color(0.49f, 0.89f, 0.78f, 0.15f));
        }
    }
}
