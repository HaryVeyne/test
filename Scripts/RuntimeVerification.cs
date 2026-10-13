using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectUnknown;

// A deliberately small integration check, run in the actual Godot main scene.
public partial class RuntimeVerification : Node
{
    public Game Game { get; init; } = null!;
    private int _checks;

    public override async void _Ready()
    {
        try
        {
            Game.StartNew();
            Game.Pause();
            var world = Game.World;
            var same = new WorldData(Game.State.Seed);
            Check(Enumerable.Range(0, 14400).All(i => world.Tiles[i % 120, i / 120] == same.Tiles[i % 120, i / 120]
                && world.Blocked[i % 120, i / 120] == same.Blocked[i % 120, i / 120]), "fixed seed determinism");
            var reachable = Reachable(world, new Vector2I(56, 87));
            foreach (var tile in new Vector2I[] { new(60,59), new(24,32), new(27,29), new(30,32), new(91,43), new(97,43), new(23,82), new(72,24), new(89,91), new(12,54), new(48,108) })
                Check(reachable.Contains(tile), $"reachable landmark {tile}");
            Check(!reachable.Contains(new Vector2I(31,82)), "tide island initially gated");

            Game.Resume();
            Game.Wanderer.Position = WorldData.Center(new Vector2I(60, 70));
            Vector2 before = Game.Wanderer.Position;
            Input.ActionPress("move_right");
            await Frames(40);
            Input.ActionRelease("move_right");
            await Frames(10);
            float walked = before.DistanceTo(Game.Wanderer.Position);
            Check(walked > 60, "native CharacterBody2D movement");
            before = Game.Wanderer.Position;
            Input.ActionPress("move_right"); Input.ActionPress("run");
            await Frames(40);
            Input.ActionRelease("move_right"); Input.ActionRelease("run");
            await Frames(10);
            Check(before.DistanceTo(Game.Wanderer.Position) > walked * 1.25f, "Shift running speed");

            Game.Wanderer.Position = WorldData.Center(new Vector2I(25,82));
            Input.ActionPress("move_right");
            await Frames(50);
            Input.ActionRelease("move_right");
            await Frames(5);
            Check(Game.Wanderer.Position.X < 26 * 32, "water collision blocks unopened bridge");
            Game.Pause();
            Game.Puzzles.UpdatePlate(WorldData.Center(new(23,82)), 0.5);
            Check(Game.Puzzles.PlateCharge > 0, "pressure plate accumulates");
            Game.Puzzles.UpdatePlate(WorldData.Center(new(23,85)), 2);
            Check(Game.Puzzles.PlateCharge == 0, "leaving plate loses charge");
            Game.Wanderer.Position = WorldData.Center(new(23,82));
            Game.Resume();
            await Frames(180);
            Check(Game.State.Activated.Contains("bridge"), "real scene dwelling raises bridge");
            Check(Reachable(world, new(56,87)).Contains(new(31,82)), "opened island reachable");

            Game.Wanderer.Position = WorldData.Center(new(25,82));
            Input.ActionPress("move_right");
            await Frames(90);
            Input.ActionRelease("move_right");
            await Frames(10);
            Check(Game.Wanderer.Position.X > 29 * 32, "native collision updates after bridge opening");
            Interact("tide", new(31,82));
            Check(Game.State.Activated.Contains("lake"), "tide beacon activation");
            Interact("wrong rune", new(24,32));
            Check(Game.State.RuneStep == 0, "wrong rune resets");
            Interact("first rune", new(27,29));
            Check(Game.State.RuneStep == 1, "first resonance persists");
            Game.Pause();
            Game.SaveGame();
            var loaded = SaveStore.Load(out string error);
            Check(loaded != null && loaded.RuneStep == 1 && loaded.Activated.Contains("lake") && loaded.Explored.Count > 0
                && Math.Abs(loaded.PlayerX - Game.Wanderer.Position.X) < 0.1f, "save restores position, fog and partial puzzle: " + error);
            Game.ContinueGame();
            Check(Game.State.RuneStep == 1 && Game.State.Activated.Contains("bridge"), "continue rebuilds active world");
            Interact("second rune", new(24,32));
            Interact("third rune", new(30,32));
            Check(Game.State.Activated.Contains("forest"), "ordered forest resonance");
            Interact("central locked", new(60,59));
            Check(!Game.State.Completed, "central requires three discoveries");
            for (int i = 0; i < 3; i++) Interact("left mirror", new(91,43));
            for (int i = 0; i < 3; i++) Interact("right mirror", new(97,43));
            Check(Game.State.Activated.Contains("dunes") && Game.State.Activated.Contains("center"), "mirrors align and central opens");
            Interact("central ending", new(60,59));
            Check(Game.State.Completed && !Game.IsPlaying, "full game completion and ending screen");
            loaded = SaveStore.Load(out error);
            Check(loaded != null && loaded.Completed && loaded.InteractionCount == Game.State.InteractionCount, "ending automatically saved");
            string export = SaveStore.Export(Game.State);
            Check(System.IO.File.Exists(export) && System.IO.File.ReadAllText(export).Contains("completed"), "JSON exploration export with result");
            Game.ReturnToMenu();
            Game.ContinueGame();
            Check(Game.State.Completed && !Game.IsPlaying, "completed save returns to ending");
            GD.Print($"VERIFICATION PASSED: {_checks} checks; export={export}");
            Game.QuitGame();
        }
        catch (Exception exception)
        {
            GD.PushError("VERIFICATION FAILED: " + exception);
            GetTree().Quit(1);
        }
    }

    private void Interact(string label, Vector2I tile)
    {
        Game.Wanderer.Position = WorldData.Center(tile);
        Game.InteractNearby();
        GD.Print("ACTION: " + label);
    }

    private async System.Threading.Tasks.Task Frames(int count)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
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
        while (queue.TryDequeue(out var at))
        foreach (var direction in new Vector2I[] { Vector2I.Up, Vector2I.Right, Vector2I.Down, Vector2I.Left })
        {
            var next = at + direction;
            if (!world.IsBlocked(next.X, next.Y) && visited.Add(next)) queue.Enqueue(next);
        }
        return visited;
    }
}
