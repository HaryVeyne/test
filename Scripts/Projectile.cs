using Godot;
using System.Collections.Generic;
using System.Linq;

namespace ProjectUnknown;

public partial class Projectile : Area2D
{
    public bool EnemyProjectile { get; private set; }
    public int Damage { get; private set; }
    public bool Magic { get; private set; }
    public bool Piercing { get; private set; }
    public Vector2 Direction { get; private set; }
    public float Speed { get; private set; }

    private CombatController _combat = null!;
    private float _radius;
    private float _life;
    private bool _done;
    private Enemy? _source;
    private readonly HashSet<Enemy> _hit = new();

    public void Configure(CombatController combat, Vector2 direction, bool enemy, int damage, float speed, float radius,
        bool magic, bool piercing, float lifetime, Enemy? source = null)
    {
        _combat = combat;
        _source = source;
        Direction = direction.Normalized();
        EnemyProjectile = enemy;
        Damage = damage;
        Speed = speed;
        _radius = radius;
        Magic = magic;
        Piercing = piercing;
        _life = lifetime;
        CollisionLayer = 0;
        CollisionMask = enemy ? 3u : 5u;
        Monitoring = true;
        Monitorable = false;
        ZIndex = 30;
        AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = radius } });
        BodyEntered += HitBody;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_done || !_combat.Game.IsPlaying) return;
        if (_source != null && !_source.IsAlive) { Expire(); return; }
        float dt = (float)delta;
        _life -= dt;
        if (_life <= 0) { Expire(); return; }
        Vector2 from = GlobalPosition;
        Vector2 to = from + Direction * Speed * dt;
        var wallQuery = PhysicsRayQueryParameters2D.Create(from, to, 1);
        var wall = GetWorld2D().DirectSpaceState.IntersectRay(wallQuery);
        if (wall.Count > 0)
        {
            GlobalPosition = (Vector2)wall["position"];
            Impact();
            return;
        }
        if (EnemyProjectile)
        {
            var player = _combat.Game.Wanderer;
            if (CombatController.DistanceToSegment(player.GlobalPosition + new Vector2(0, 3), from, to) <= _radius + 8)
            {
                GlobalPosition = to;
                _combat.DamagePlayer(Damage, from - Direction * 20);
                Impact();
                return;
            }
        }
        else
        {
            foreach (var enemy in _combat.Enemies.ToArray())
            {
                if (!enemy.IsAlive || _hit.Contains(enemy)) continue;
                if (CombatController.DistanceToSegment(enemy.GlobalPosition, from, to) > _radius + (enemy.IsBoss ? 22 : 10)) continue;
                GlobalPosition = to;
                HitEnemy(enemy);
                if (_done) return;
            }
        }
        GlobalPosition = to;
        QueueRedraw();
    }

    private void HitBody(Node2D body)
    {
        if (_done || !_combat.Game.IsPlaying || _source?.IsAlive == false) return;
        if (EnemyProjectile && body is Player)
        {
            _combat.DamagePlayer(Damage, GlobalPosition - Direction * 20);
            Impact();
        }
        else if (!EnemyProjectile && body is Enemy enemy && enemy.IsAlive && !_hit.Contains(enemy))
            HitEnemy(enemy);
        else if (body is StaticBody2D) Impact();
    }

    private void HitEnemy(Enemy enemy)
    {
        _hit.Add(enemy);
        if (Magic)
        {
            _combat.SpawnEffect(GlobalPosition, 55, new Color("93dfc8"), 0.42f);
            foreach (var target in _combat.Enemies.ToArray())
                if (target.IsAlive && target.GlobalPosition.DistanceTo(GlobalPosition) <= 64 && _combat.HasLineOfSight(GlobalPosition, target.GlobalPosition))
                {
                    _hit.Add(target);
                    target.TakeDamage(target == enemy ? Damage : (int)(Damage * 0.7f), (target.GlobalPosition - GlobalPosition).Normalized() * 85);
                }
        }
        else enemy.TakeDamage(Damage, Direction * 80);
        if (!Piercing) Impact();
        else _combat.SpawnEffect(enemy.GlobalPosition, 14, new Color("dae9ab"), 0.18f);
    }

    private void Impact()
    {
        if (_done) return;
        _combat.SpawnEffect(GlobalPosition, Magic ? 26 : 10, EnemyProjectile ? new Color("ec9b75") : new Color("b8dfd3"), 0.22f);
        Expire();
    }

    public void Expire()
    {
        if (_done) return;
        _done = true;
        SetDeferred(Area2D.PropertyName.Monitoring, false);
        _combat.RemoveProjectile(this);
    }

    public override void _Draw()
    {
        Color color = EnemyProjectile ? new Color("ef9b74") : Magic ? new Color("95e6ca") : new Color("e0dbaf");
        if (Magic)
        {
            DrawCircle(Vector2.Zero, _radius * 2.4f, new Color(color, 0.16f));
            DrawCircle(Vector2.Zero, _radius, color);
            DrawCircle(-Direction * 8, _radius * 0.6f, new Color(color, 0.55f));
            DrawCircle(-Direction * 14, _radius * 0.35f, new Color(color, 0.25f));
        }
        else
        {
            DrawLine(-Direction * 14, Direction * 3, color, EnemyProjectile ? 3 : 2);
            DrawLine(Direction * 3, -Direction * 3 + Direction.Orthogonal() * 4, color, 2);
            DrawLine(Direction * 3, -Direction * 3 - Direction.Orthogonal() * 4, color, 2);
            if (Piercing) DrawCircle(Vector2.Zero, 7, new Color(0.64f, 0.85f, 0.96f, 0.16f));
        }
    }
}
