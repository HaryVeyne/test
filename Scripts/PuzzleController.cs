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
        Note("我在湖边石盘上停留了一会儿。水面下升起了一条石桥，通向岛上的幽光。重量与时间似乎都参与了回应。");
        Record("mechanism", "bridge: pressure plate held; stone bridge emerged");
        return new("石盘下传来低鸣。湖面浮出一道石桥。", true, true);
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
                if (_state.Completed) return new("遗迹的光仍在呼吸。世界已经记住了你的足迹。", true);
                if (!_state.Activated.Contains("center"))
                {
                    Note("中央遗迹有三道未亮的纹路：叶脉、涟漪与日轮。沟槽分别向林地、湖泊与荒漠伸展。这些地方的回应也许能汇合在此。");
                    return new("石门沉默。叶脉、涟漪与日轮在门环上若隐若现。");
                }
                _state.Completed = true;
                Note("三股回应在遗迹汇合。我触碰核心时，整片陌生的土地亮起了相连的脉络。终点从来没有藏在远方，它藏在我逐渐理解的联系里。");
                Record("ending", "completed: forest, lake and dunes connected at central ruin");
                return new("你让这片土地重新记起了彼此。", true, true);

            case "tide_beacon":
                if (!_state.Activated.Contains("bridge")) return new("幽光隔着水面，触手难及。");
                if (_state.Activated.Contains("lake")) return new("信标的涟漪仍指向远方的遗迹。", true);
                _state.Activated.Add("lake");
                Note("湖心信标亮起了涟漪状的纹路。一束微光沿水面向中央遗迹延伸，那里有同样的图案。");
                Record("mechanism", "lake: tide beacon awakened");
                return CompleteConnection("湖心的涟漪向中央遗迹缓缓扩散。");

            case "plate":
                Note("湖边石盘的外圈会在我站上去时积起光。踏离之后，未成形的光又慢慢消退。盘边没有需要按下的按钮。");
                return new(_state.Activated.Contains("bridge") ? "石盘已沉稳地嵌入地面。桥仍立在水上。" : "石盘随你的重量微微下沉，外圈的光开始汇聚。");

            case "overlook":
                Note("高处的残图描着三条曲线，分别来自树冠、湖面与沙丘。它们在中央的圆环相交。图中没有标明旅人应从哪里开始。");
                return new("残图上，三条互不相同的曲线在一个圆环相交。");
            case "archive":
                Note("旧档案留下一句话：『听见次序，看见方向，等待重量。三个回应，仍是同一世界。』余下的文字已被风沙磨平。");
                return new("『听见次序，看见方向，等待重量。』碑文至此断裂。");
            case "hollow":
                Note("空树中的刻画与林间符文相似。三个印记的刻痕数不同；画中的光先后经过它们，似乎不是按距离选择顺序。");
                return new("空树中的三个印记，各自带着不同数量的短刻痕。");
            case "bell":
                Note("我敲响了南方的残钟。三个清亮的泛音之后，一声更深的共鸣从北方传来。这片土地仍有许多不必解开的秘密。");
                Record("optional", "bell: resonant chime heard");
                return new("钟声沿草地回荡。远处传来一道更深的回声。", true);
            case "camp":
                Note("旧营地里只有一张褪色的纸：『不要急着寻找出口。让看见的事互相回答。』我把它留在这里，开始自己的记录。");
                return new("『让看见的事互相回答。』这是纸上最后一句话。");
            default:
                return new("风经过石缝，暂时没有别的回应。");
        }
    }

    private PuzzleFeedback TouchRune(int index)
    {
        if (_state.Activated.Contains("forest")) return new("三块符文石正以同一节律呼吸。", true);
        if (index != RuneOrder[_state.RuneStep])
        {
            _state.RuneStep = 0;
            Note("一次触碰让林间累积的光同时熄灭。符文不是各自独立的开关。石上的短刻痕也许与它们等待的次序有关。");
            Record("mechanism", $"forest: rune {index} broke the resonance; sequence reset");
            return new("一声不和谐的低响，先前的光全部熄灭。", false, true);
        }

        _state.RuneStep++;
        Record("mechanism", $"forest: rune {index} accepted; step {_state.RuneStep}/3");
        if (_state.RuneStep < RuneOrder.Length)
        {
            Note("符文石回应了我的触碰，光留在石中；另一块石头似乎正等待下一次回应。每块石旁的短刻痕数量不同。");
            return new("符文留住了这道光。林间还有回应尚未到来。", true, true);
        }

        _state.Activated.Add("forest");
        Note("林间三块符文按某种次序连成了完整的鸣响。叶脉纹路亮起，光的方向指向中央遗迹的相同纹章。");
        return CompleteConnection("三道符文连成一道叶脉般的光，指向中央。");
    }

    private PuzzleFeedback TurnMirror(int index)
    {
        if (_state.Activated.Contains("dunes")) return new("两道镜光交汇在日轮印记上。", true);
        _state.Mirrors[index] = (_state.Mirrors[index] + 1) % 4;
        Record("mechanism", $"dunes: mirror {index} rotated to {_state.Mirrors[index]}");
        Note("沙丘中的古镜可以转动。镜光只朝它面向的方向延伸，而两座基台之间有一个破碎的日轮印记。");
        if (_state.Mirrors[0] != 0 || _state.Mirrors[1] != 2)
            return new("古镜转过四分之一圈。一束微光随之改变方向。", false, true);

        _state.Activated.Add("dunes");
        Note("两道古镜的光在中间相会，沙上的日轮重新完整。它的光沿地下纹路向中央遗迹行进。");
        Record("mechanism", "dunes: mirrors aligned; solar seal awakened");
        return CompleteConnection("镜光在破碎日轮上相会，荒漠传来温暖的共鸣。");
    }

    private PuzzleFeedback CompleteConnection(string message)
    {
        if (_state.Activated.Contains("forest") && _state.Activated.Contains("lake") && _state.Activated.Contains("dunes") && _state.Activated.Add("center"))
        {
            Note("叶脉、涟漪与日轮终于同时回应。三股光在中央遗迹汇合，远方传来石门开启的声音。我想，现在可以再去看一眼。");
            Record("goal", "central ruin unlocked: all three regional seals awakened");
            return new(message + " 远方的中央遗迹随之开启。", true, true);
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
            "camp" => "这里曾有人停留过，草地上还有一张旧纸。",
            _ => "这里留着一段尚未读懂的痕迹。"
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
        if (_state.Completed) hint = "旅程已结束，世界的其他角落仍留下了未读的痕迹。";
        else if (_state.Activated.Contains("center")) hint = "曾经沉默的中央遗迹，现在也许有了不同的回应。";
        else if (_state.Discovered.Count < 3) hint = count < 2
            ? "道路的尽头、树木让出的空地和孤独的石头，常常值得停下脚步。"
            : "从旧营地向不同方向走走。地图会记住你看过的地方，日志会记住不寻常的回应。";
        else if (_state.Discovered.Contains("forest") && !_state.Activated.Contains("forest")) hint = count < 3
            ? "林间的石头并不相同。光会保留什么，又会因什么消退？"
            : "把视线从符文本身移到周围：短刻痕的数目，可能比石头离你的距离更有意义。";
        else if (_state.Discovered.Contains("lake") && !_state.Activated.Contains("lake")) hint = count < 3
            ? "有些装置需要的是停留。站上石盘时，可以观察光是否在慢慢变化。"
            : "湖边的石盘会积起光；新的通路出现之后，别忘了看看它通向的幽光。";
        else if (_state.Discovered.Contains("dunes") && !_state.Activated.Contains("dunes")) hint = count < 3
            ? "光的方向也是一种语言。两道光之间，地面画了什么？"
            : "古镜能改变照射方向。注意两座基台之间的残图能否同时收到光。";
        else hint = "中央门环上的纹章来自不同地方。还没有回应的那一道，也许正等着你在别处发现。";
        Record("hint", hint);
        return hint;
    }
}
