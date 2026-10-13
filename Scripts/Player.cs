using Godot;
using System;

namespace ProjectUnknown;

public partial class Player : CharacterBody2D
{
    public bool Active { get; set; }
    public event Action<float, bool>? Travelled;
    private bool _wasMoving;
    private Vector2 _facing = Vector2.Down;
    private float _stride;

    public override void _Ready()
    {
        CollisionLayer = 2;
        CollisionMask = 1;
        AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 7 }, Position = new Vector2(0, 3) });
        ZIndex = 20;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Active)
        {
            Velocity = Vector2.Zero;
            _wasMoving = false;
            return;
        }
        var direction = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        float speed = Input.IsActionPressed("run") ? 220 : 140;
        Velocity = Velocity.MoveToward(direction * speed, (float)delta * 1100);
        var before = Position;
        MoveAndSlide();
        float distance = before.DistanceTo(Position);
        bool moving = distance > 0.03f;
        if (moving)
        {
            _facing = Velocity.Normalized();
            _stride += distance * 0.095f;
            Travelled?.Invoke(distance, !_wasMoving);
        }
        _wasMoving = moving;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float bob = Velocity.LengthSquared() > 10 ? Mathf.Sin(_stride) * 1.3f : 0;
        DrawRect(new Rect2(-9, 7, 18, 5), new Color(0.02f, 0.06f, 0.06f, 0.35f));
        var coat = new Color("d4ba87");
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
        // A small warm lantern gives the wanderer a clear silhouette.
        var lantern = _facing.X < -0.3f ? new Vector2(-11, 0) : new Vector2(11, 0);
        DrawCircle(lantern, 8, new Color(1, 0.76f, 0.36f, 0.08f));
        DrawRect(new Rect2(lantern + new Vector2(-2, -3), new Vector2(4, 6)), new Color("f4da94"));
    }
}
