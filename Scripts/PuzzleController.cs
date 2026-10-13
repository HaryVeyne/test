using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectUnknown;

public sealed record PuzzleFeedback(string Message, bool Success = false, bool Changed = false);

public sealed class PuzzleController
{
    private readonly GameState _state;
    private static readonly int[] RuneOrder = { 1, 0, 2 };
    private static readonly Dictionary<string, Vector2I> Sites = new()
    {
        ["center"] = new(60, 59),
        ["rune_0"] = new(24, 32),
        ["rune_1"] = new(27, 29),
        ["rune_2"] = new(30, 32),
        ["mirror_0"] = new(91, 43),
        ["mirror_1"] = new(97, 43),
        ["plate"] = new(23, 82),
        ["tide_beacon"] = new(31, 82),
        ["overlook"] = new(72, 24),
        ["archive"] = new(89, 91),
        ["hollow"] = new(12, 54),
        ["bell"] = new(48, 108),
        ["camp"] = new(56, 85)
    };

    public float PlateCharge { get; private set; }

    public PuzzleController(GameState state)
    {
        _state = state;
        PlateCharge = state.Activated.Contains("bridge") ? 1 : 0;
    }

    public PuzzleFeedback? UpdatePlate(Vector2 pos, double delta)
    {
        if (_state.Activated.Contains("bridge")) return null;
        bool onPlate = pos.DistanceSquaredTo(WorldData.Center(Sites["plate"])) <= 28 * 28;
        PlateCharge = Mathf.Clamp(PlateCharge + (float)delta * (onPlate ? 1f / 2.4f : -0.6f), 0, 1);
        if (PlateCharge < 1) return null;

        _state.Activated.Add("bridge");
        Note("在湖边石盘上停留后，石桥升起。可前往湖心岛。");
        Record("mechanism", "bridge: pressure plate held; stone bridge emerged");
        return new("石桥已升起。", true, true);
    }

    public string? Nearest(Vector2 pos)
    {
        string? nearest = null;
        float distance = 48 * 48;
        foreach (var site in Sites)
        {
            float candidate = pos.DistanceSquaredTo(WorldData.Center(site.Value));
            if (candidate > distance) continue;
            distance = candidate;
            nearest = site.Key;
        }
        return nearest;
    }

    public string Prompt(string id) => id switch
    {
        "center" when _state.Activated.Contains("center") => "E · 步入遗迹核心",
        "center" => "E · 观察遗迹",
        "rune_0" or "rune_1" or "rune_2" => "E · 触碰符文石",
        "mirror_0" or "mirror_1" => "E · 旋转古镜",
        "plate" => "E · 查看石盘",
        "tide_beacon" => "E · 触碰湖心信标",
        "camp" => "E · 查看旧营地",
        "bell" => "E · 敲响残钟",
        _ => "E · 阅读遗留痕迹"
    };

    public PuzzleFeedback Interact(string id)
    {
        _state.InteractionCount++;
        Record("interact", id);
        if (id.StartsWith("rune_", StringComparison.Ordinal)) return TouchRune(id[^1] - '0');
        if (id.StartsWith("mirror_", StringComparison.Ordinal)) return TurnMirror(id[^1] - '0');

        switch (id)
        {
            case "center":
                if (_state.Completed) return new("中央遗迹已完成。", true);
                if (!_state.Activated.Contains("center"))
                {
                    Note("门环有叶脉、涟漪和日轮纹章，沟槽分别连接林地、湖泊和荒漠。");
                    return new("门环上的三道纹章尚未全部点亮。");
                }
                _state.Completed = true;
                Note("三个地区的装置已接通，中央遗迹已启动。");
                Record("ending", "completed: forest, lake and dunes connected at central ruin");
                return new("中央遗迹已启动。", true, true);

            case "tide_beacon":
                if (!_state.Activated.Contains("bridge")) return new("需要先升起石桥。");
                if (_state.Activated.Contains("lake")) return new("湖心信标已激活。", true);
                _state.Activated.Add("lake");
                Note("湖心信标已激活，中央遗迹的涟漪纹章亮起。");
                Record("mechanism", "lake: tide beacon awakened");
                return CompleteConnection("湖心信标已激活。");

            case "plate":
                Note("站在石盘上时光环会逐渐充满；离开后进度消退。");
                return new(_state.Activated.Contains("bridge") ? "石桥已升起。" : "光环随踩踏时间逐渐充满。");

            case "overlook":
                Note("残图标记了林地、湖泊和沙丘，三条线路连接中央遗迹。");
                return new("残图显示三个地区的装置连接中央遗迹。");
            case "archive":
                Note("档案记录：符文需按次序激活，古镜需调整方向，石盘需持续踩踏。");
                return new("档案：符文有次序，古镜有方向，石盘需要持续踩踏。");
            case "hollow":
                Note("树洞中的刻画与林间符文相同。每个印记旁的刻痕数量不同。");
                return new("刻画中的三个印记带有不同数量的刻痕。");
            case "bell":
                Note("敲响南方残钟后，北侧遗迹传来回声。");
                Record("optional", "bell: resonant chime heard");
                return new("北侧遗迹传来回声。", true);
            case "camp":
                Note("营地记录：林地有符文装置，湖边有石盘，沙丘有古镜。");
                return new("营地记录了林地、湖泊和沙丘的装置。");
            default:
                return new("没有可互动的装置。");
        }
    }

    private PuzzleFeedback TouchRune(int index)
    {
        if (_state.Activated.Contains("forest")) return new("林地符文已激活。", true);
        if (index != RuneOrder[_state.RuneStep])
        {
            _state.RuneStep = 0;
            Note("符文顺序错误，进度重置。石旁刻痕数量与次序有关。");
            Record("mechanism", $"forest: rune {index} broke the resonance; sequence reset");
            return new("符文熄灭，进度重置。", false, true);
        }

        _state.RuneStep++;
        Record("mechanism", $"forest: rune {index} accepted; step {_state.RuneStep}/3");
        if (_state.RuneStep < RuneOrder.Length)
        {
            Note("符文保留了光，次序尚未完成。石旁刻痕数量不同。");
            return new("符文已点亮。", true, true);
        }

        _state.Activated.Add("forest");
        Note("三块符文已激活，中央遗迹的叶脉纹章亮起。");
        return CompleteConnection("林地装置已接通。");
    }

    private PuzzleFeedback TurnMirror(int index)
    {
        if (_state.Activated.Contains("dunes")) return new("古镜已对准日轮。", true);
        _state.Mirrors[index] = (_state.Mirrors[index] + 1) % 4;
        Record("mechanism", $"dunes: mirror {index} rotated to {_state.Mirrors[index]}");
        Note("古镜每次旋转四分之一圈，光束随方向改变。两座古镜之间有日轮印记。");
        if (_state.Mirrors[0] != 0 || _state.Mirrors[1] != 2)
            return new("古镜已旋转，光束方向改变。", false, true);

        _state.Activated.Add("dunes");
        Note("两道镜光在日轮相会，中央遗迹的日轮纹章亮起。");
        Record("mechanism", "dunes: mirrors aligned; solar seal awakened");
        return CompleteConnection("荒漠装置已接通。");
    }

    private PuzzleFeedback CompleteConnection(string message)
    {
        if (_state.Activated.Contains("forest") && _state.Activated.Contains("lake") && _state.Activated.Contains("dunes") && _state.Activated.Add("center"))
        {
            Note("三个地区的装置已接通，中央遗迹可以启动。");
            Record("goal", "central ruin unlocked: all three regional seals awakened");
            return new(message + " 中央遗迹已解锁。", true, true);
        }
        return new(message, true, true);
    }

    public void Discover(string id, string name)
    {
        if (!_state.Discovered.Add(id)) return;
        Note($"发现：{name}。" + (id switch
        {
            "forest" => "树木在三块带刻痕的石头周围让出了一片空地。",
            "dunes" => "两座古镜在沙丘中隔着一个日轮图案相对。",
            "lake" => "湖边的石盘对面，一座小岛仍闪着幽光。",
            "center" => "三种不同纹章刻在同一座门环上。",
            "camp" => "营地可休息和设置复活点。",
            _ => "已标记在地图上。"
        }));
        Record("discover", $"{id}: {name}");
    }

    private void Note(string text)
    {
        if (_state.Journal.Any(entry => entry.Text == text)) return;
        _state.Journal.Add(new(_state.PlayedSeconds, text));
    }

    public void Record(string action, string detail) =>
        _state.Events.Add(new(_state.PlayedSeconds, action, detail));

    public string Hint()
    {
        int count = _state.Events.Count(entry => entry.Action == "hint");
        string hint;
        if (_state.Completed) hint = "中央遗迹已完成。可前往林地墓室和熔炉挑战守卫。";
        else if (_state.Activated.Contains("center")) hint = "三个装置已接通，可回中央遗迹启动核心。";
        else if (_state.Discovered.Count < 3) hint = count < 2
            ? "沿道路寻找符文石、湖边石盘和沙丘古镜。"
            : "林地位于西北，湖泊位于西侧，古镜位于东北。";
        else if (_state.Discovered.Contains("forest") && !_state.Activated.Contains("forest")) hint = count < 3
            ? "注意符文点亮后是否保持，以及错误触碰的变化。"
            : "比较符文石旁的刻痕数量。";
        else if (_state.Discovered.Contains("lake") && !_state.Activated.Contains("lake")) hint = count < 3
            ? "站上湖边石盘时，观察光环的变化。"
            : "石桥升起后，可到岛上触碰信标。";
        else if (_state.Discovered.Contains("dunes") && !_state.Activated.Contains("dunes")) hint = count < 3
            ? "观察古镜之间的地面印记和光束方向。"
            : "尝试让两道镜光同时照到中央印记。";
        else hint = "检查中央门环尚未点亮的纹章，并寻找对应地区的装置。";
        Record("hint", hint);
        return hint;
    }
}
