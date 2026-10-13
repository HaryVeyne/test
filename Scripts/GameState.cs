using System.Collections.Generic;

namespace ProjectUnknown;

public sealed class GameState
{
    public int Version { get; set; } = 1;
    public int Seed { get; set; } = 73129;
    public float PlayerX { get; set; }
    public float PlayerY { get; set; }
    public double PlayedSeconds { get; set; }
    public float Distance { get; set; }
    public int MoveCount { get; set; }
    public int InteractionCount { get; set; }
    public bool Completed { get; set; }
    public HashSet<int> Explored { get; set; } = new();
    public HashSet<string> Activated { get; set; } = new();
    public HashSet<string> Discovered { get; set; } = new();
    public HashSet<string> Regions { get; set; } = new();
    public List<JournalEntry> Journal { get; set; } = new();
    public List<ExplorationEvent> Events { get; set; } = new();
    public int RuneStep { get; set; }
    public int[] Mirrors { get; set; } = new[] { 1, 3 };
}

public sealed record JournalEntry(double Time, string Text);

public sealed record ExplorationEvent(double Time, string Action, string Detail);
