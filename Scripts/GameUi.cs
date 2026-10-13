using Godot;
using System;
using System.Linq;

namespace ProjectUnknown;

public partial class GameUi : CanvasLayer
{
    private static readonly Color Ink = new("101b22");
    private static readonly Color Paper = new("e8e6d7");
    private static readonly Color Muted = new("9caeac");
    private static readonly Color Gold = new("d6b975");
    private static readonly Color Cyan = new("82c5c0");
    private Game _game = null!;
    private Control _root = null!, _hud = null!, _overlay = null!, _main = null!;
    private CenterContainer _pageHost = null!;
    private PanelContainer _pause = null!, _settings = null!, _map = null!, _journal = null!, _ending = null!;
    private Label _region = null!, _progress = null!, _prompt = null!, _toastText = null!, _mapStats = null!;
    private PanelContainer _promptPanel = null!, _toastPanel = null!;
    private Button _continue = null!, _hint = null!;
    private VBoxContainer _journalEntries = null!, _endingStats = null!;
    private CheckBox _hints = null!;
    private HSlider _volume = null!;
    private MapView _mapView = null!;
    private string _active = "main", _settingsFrom = "main";
    private double _toastTime;

    public bool HasOverlay => _active.Length != 0;

    public void Configure(Game game)
    {
        _game = game;
        Layer = 10;
        ProcessMode = ProcessModeEnum.Always;
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(_root);
        Fill(_root);
        _root.Theme = MakeTheme();
        BuildHud();
        BuildOverlay();
        BuildMainMenu();
        BuildPause();
        BuildSettings();
        BuildMap();
        BuildJournal();
        BuildEnding();
        BuildToast();
        ShowMainMenu();
    }

    private Theme MakeTheme()
    {
        var theme = new Theme { DefaultFontSize = 18 };
        const string path = "res://Assets/Fonts/NotoSansSC-Regular.otf";
        if (ResourceLoader.Exists(path)) theme.DefaultFont = GD.Load<Font>(path);
        theme.SetColor("font_color", "Label", Paper);
        theme.SetColor("font_color", "Button", Paper);
        theme.SetColor("font_hover_color", "Button", Ink);
        theme.SetColor("font_pressed_color", "Button", Ink);
        theme.SetColor("font_disabled_color", "Button", new Color("566562"));
        theme.SetColor("font_color", "CheckBox", Paper);
        theme.SetStylebox("normal", "Button", Box(new Color("24323a"), new Color("53635e"), 7, 18, 12));
        theme.SetStylebox("hover", "Button", Box(Gold, Gold, 7, 18, 12));
        theme.SetStylebox("pressed", "Button", Box(new Color("bba16a"), Gold, 7, 18, 12));
        theme.SetStylebox("disabled", "Button", Box(new Color("1c282e"), new Color("34423f"), 7, 18, 12));
        theme.SetStylebox("focus", "Button", Box(Colors.Transparent, Cyan, 7, 0, 0, 2));
        theme.SetStylebox("panel", "PanelContainer", Box(new Color("111d25", 0.97f), new Color("506058"), 12, 30, 24));
        theme.SetConstant("separation", "VBoxContainer", 13);
        theme.SetConstant("separation", "HBoxContainer", 14);
        theme.SetStylebox("slider", "HSlider", Box(new Color("344840"), new Color("344840"), 4, 0, 3));
        theme.SetStylebox("grabber_area", "HSlider", Box(Gold, Gold, 4, 0, 3));
        theme.SetStylebox("grabber_area_highlight", "HSlider", Box(Cyan, Cyan, 4, 0, 3));
        return theme;
    }

    private static StyleBoxFlat Box(Color fill, Color border, int radius = 6, int horizontal = 16, int vertical = 12, int borderSize = 1)
    {
        return new StyleBoxFlat
        {
            BgColor = fill, BorderColor = border,
            BorderWidthLeft = borderSize, BorderWidthRight = borderSize, BorderWidthTop = borderSize, BorderWidthBottom = borderSize,
            CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
            ContentMarginLeft = horizontal, ContentMarginRight = horizontal, ContentMarginTop = vertical, ContentMarginBottom = vertical
        };
    }

    private static void Fill(Control control) => control.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    private static Label Text(string value, int size = 18, Color? color = null)
    {
        var label = new Label { Text = value, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        if (color.HasValue) label.AddThemeColorOverride("font_color", color.Value);
        return label;
    }
    private static Label Paragraph(string value, int size = 17, Color? color = null)
    {
        var label = Text(value, size, color ?? Muted);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return label;
    }
    private static Button Button(string label, Action action)
    {
        var button = new Button { Text = label, CustomMinimumSize = new Vector2(0, 46), MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        button.Pressed += action;
        return button;
    }
    private static ColorRect Line(Color? color = null)
    {
        return new ColorRect { Color = color ?? new Color("40514b"), CustomMinimumSize = new Vector2(0, 1), MouseFilter = Control.MouseFilterEnum.Ignore };
    }
    private static Control Space(float height = 12) => new() { CustomMinimumSize = new Vector2(0, height), MouseFilter = Control.MouseFilterEnum.Ignore };

    private void BuildHud()
    {
        _hud = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.AddChild(_hud);
        Fill(_hud);
        var topLeft = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(28, 24) };
        topLeft.AddThemeStyleboxOverride("panel", Box(new Color("101b22", 0.86f), new Color("536052", 0.7f), 8, 18, 10));
        _hud.AddChild(topLeft);
        var area = new VBoxContainer();
        area.AddThemeConstantOverride("separation", 2);
        topLeft.AddChild(area);
        area.AddChild(Text("P R O J E C T   U N K N O W N", 12, Gold));
        _region = Text("陌生的草地", 23);
        area.AddChild(_region);

        var topRight = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        topRight.AddThemeStyleboxOverride("panel", Box(new Color("101b22", 0.86f), new Color("536052", 0.7f), 8, 18, 10));
        _hud.AddChild(topRight);
        topRight.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        topRight.OffsetLeft = -322; topRight.OffsetRight = -28; topRight.OffsetTop = 24; topRight.OffsetBottom = 88;
        var right = new VBoxContainer();
        right.AddThemeConstantOverride("separation", 3);
        topRight.AddChild(right);
        _progress = Text("遗迹信号  ·  ·  ·", 18, Gold);
        right.AddChild(_progress);
        right.AddChild(Text("M  地图    J  日志    Esc  暂停", 13, Muted));

        var controls = Text("WASD / 方向键  移动    Shift  奔跑", 14, Paper);
        _hud.AddChild(controls);
        controls.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft);
        controls.OffsetLeft = 30; controls.OffsetTop = -37; controls.OffsetBottom = -17;
        controls.AddThemeColorOverride("font_shadow_color", Ink);
        controls.AddThemeConstantOverride("shadow_offset_x", 1);
        controls.AddThemeConstantOverride("shadow_offset_y", 2);

        _promptPanel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _promptPanel.AddThemeStyleboxOverride("panel", Box(new Color("101b22", 0.9f), Gold, 7, 20, 10));
        _hud.AddChild(_promptPanel);
        _promptPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom);
        _promptPanel.OffsetLeft = -190; _promptPanel.OffsetRight = 190; _promptPanel.OffsetTop = -100; _promptPanel.OffsetBottom = -50;
        _prompt = Text("E  观察", 19);
        _prompt.HorizontalAlignment = HorizontalAlignment.Center;
        _promptPanel.AddChild(_prompt);
    }

    private void BuildOverlay()
    {
        _overlay = new Control();
        _root.AddChild(_overlay);
        Fill(_overlay);
        var shade = new ColorRect { Color = new Color("071119", 0.76f) };
        _overlay.AddChild(shade);
        Fill(shade);
        _pageHost = new CenterContainer();
        _overlay.AddChild(_pageHost);
        Fill(_pageHost);
    }

    private PanelContainer Page(Vector2 minimum, out VBoxContainer body)
    {
        var panel = new PanelContainer { CustomMinimumSize = minimum, Visible = false };
        _pageHost.AddChild(panel);
        body = new VBoxContainer();
        panel.AddChild(body);
        return panel;
    }

    private void Header(VBoxContainer body, string tag, string title, string subtitle = "")
    {
        body.AddChild(Text(tag, 12, Gold));
        body.AddChild(Text(title, 30));
        if (subtitle.Length > 0) body.AddChild(Paragraph(subtitle, 16));
        body.AddChild(Line());
    }

    private void BuildMainMenu()
    {
        _main = new Control();
        _overlay.AddChild(_main);
        Fill(_main);
        var margin = new MarginContainer();
        _main.AddChild(margin);
        Fill(margin);
        margin.AddThemeConstantOverride("margin_left", 72);
        margin.AddThemeConstantOverride("margin_right", 72);
        margin.AddThemeConstantOverride("margin_top", 54);
        margin.AddThemeConstantOverride("margin_bottom", 40);
        var rows = new VBoxContainer();
        margin.AddChild(rows);
        rows.AddChild(Text("一 个 没 有 指 路 标 的 世 界", 14, Gold));
        var columns = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 60);
        rows.AddChild(columns);
        var titleColumn = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        columns.AddChild(titleColumn);
        titleColumn.AddChild(Text("PROJECT", 60, Paper));
        var unknown = Text("UNKNOWN", 60, Paper);
        unknown.AddThemeConstantOverride("outline_size", 0);
        titleColumn.AddChild(unknown);
        titleColumn.AddChild(Space(8));
        titleColumn.AddChild(Text("未 知 之 境", 34, Gold));
        titleColumn.AddChild(Space(14));
        titleColumn.AddChild(Paragraph("风穿过旧石。\n有人来过这里，留下了并不完整的回答。", 20, Paper));
        titleColumn.AddChild(Space(6));
        titleColumn.AddChild(Paragraph("观察、尝试、记住。\n路没有终点的标记，世界却会回应你。", 17));
        var menuColumn = new VBoxContainer { CustomMinimumSize = new Vector2(354, 0), Alignment = BoxContainer.AlignmentMode.Center };
        columns.AddChild(menuColumn);
        var card = new PanelContainer();
        menuColumn.AddChild(card);
        var choices = new VBoxContainer();
        card.AddChild(choices);
        choices.AddChild(Text("踏入这片陌生的土地", 21));
        choices.AddChild(Paragraph("一段单人探索旅程  /  V0.1", 14));
        choices.AddChild(Space(3));
        var begin = Button("新游戏   →", () => _game.StartNew());
        choices.AddChild(begin);
        _continue = Button("继续探索", () => _game.ContinueGame());
        choices.AddChild(_continue);
        choices.AddChild(Button("设置", () => ShowSettings("main")));
        choices.AddChild(Button("退出游戏", () => _game.QuitGame()));
        choices.AddChild(Space(1));
        choices.AddChild(Paragraph("WASD 移动 · E 与世界互动\n地图与日志会记住你的发现。", 13));
        var footer = new HBoxContainer();
        rows.AddChild(footer);
        var version = Text("V0.1     WINDOWS PC     SINGLE PLAYER", 12, Muted);
        version.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        footer.AddChild(version);
        footer.AddChild(Text("F11  全屏 / 窗口", 13, Muted));
    }

    private void BuildPause()
    {
        _pause = Page(new Vector2(440, 0), out var body);
        Header(body, "TAKE A BREATH", "旅程暂停", "你的发现已留在日志里。慢慢来。");
        body.AddChild(Button("继续探索   Esc", CloseOverlay));
        body.AddChild(Button("保存进度", () => _game.SaveGame()));
        body.AddChild(Button("探索日志   J", ToggleJournal));
        body.AddChild(Button("设置", () => ShowSettings("pause")));
        body.AddChild(Button("导出探索记录  JSON", () => _game.ExportLog()));
        body.AddChild(Button("返回主菜单", () => _game.ReturnToMenu()));
        body.AddChild(Button("保存并退出", () => _game.QuitGame()));
    }

    private void BuildSettings()
    {
        _settings = Page(new Vector2(580, 0), out var body);
        Header(body, "MAKE YOURSELF AT HOME", "设置");
        body.AddChild(Text("环境与机关音量", 18));
        _volume = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.01, Value = _game.Volume, CustomMinimumSize = new Vector2(0, 28) };
        _volume.ValueChanged += value => _game.SetVolume((float)value);
        body.AddChild(_volume);
        body.AddChild(Space(5));
        _hints = new CheckBox { Text = "允许渐进式提示", ButtonPressed = _game.HintsEnabled };
        _hints.Toggled += value => _game.SetHints(value);
        body.AddChild(_hints);
        body.AddChild(Paragraph("提示只在你主动请求时出现，帮助你重新观察线索。", 15));
        body.AddChild(Line());
        body.AddChild(Button("切换全屏 / 窗口   F11", () => _game.ToggleFullscreen()));
        body.AddChild(Paragraph("支持调整窗口大小。设置会保存在本机。", 14));
        body.AddChild(Button("返回   Esc", CloseOverlay));
    }

    private void BuildMap()
    {
        _map = Page(new Vector2(960, 620), out var body);
        var header = new HBoxContainer();
        body.AddChild(header);
        var title = Text("逐渐显露的世界", 27);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        header.AddChild(title);
        header.AddChild(Button("关闭   M / Esc", CloseOverlay));
        _mapStats = Text("", 15, Muted);
        body.AddChild(_mapStats);
        _mapView = new MapView { CustomMinimumSize = new Vector2(0, 430), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddChild(_mapView);
        _mapView.CustomMinimumSize = new Vector2(650, 430);
        var legend = Text("草地  ·  林地  ·  沙丘  ·  水域  ·  遗迹     /     金色：已发现地点     青色：已回应     浅色箭头：你", 14, Muted);
        legend.HorizontalAlignment = HorizontalAlignment.Center;
        body.AddChild(legend);
    }

    private void BuildJournal()
    {
        _journal = Page(new Vector2(880, 610), out var body);
        var header = new HBoxContainer();
        body.AddChild(header);
        var title = Text("探索日志", 30);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        header.AddChild(title);
        header.AddChild(Button("关闭   J / Esc", CloseOverlay));
        body.AddChild(Paragraph("把散落的线索放在一起。有些变化，发生在视线之外。", 16));
        body.AddChild(Line());
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 365), SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        body.AddChild(scroll);
        _journalEntries = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _journalEntries.AddThemeConstantOverride("separation", 16);
        scroll.AddChild(_journalEntries);
        body.AddChild(Line());
        var footer = new HBoxContainer();
        body.AddChild(footer);
        _hint = Button("我需要一点线索", () => _game.RequestHint());
        _hint.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        footer.AddChild(_hint);
        footer.AddChild(Button("导出探索记录", () => _game.ExportLog()));
    }

    private void BuildEnding()
    {
        _ending = Page(new Vector2(870, 620), out var body);
        body.AddThemeConstantOverride("separation", 8);
        Header(body, "THE WORLD REMEMBERS", "未知之境 · 已被回应", "当三道回声在旧石间相遇，遗迹终于认出了你的脚步。");
        body.AddChild(Paragraph("你并非找到了所有答案。\n你学会了聆听这个世界，而它向你打开了第一扇门。", 18, Paper));
        _endingStats = new VBoxContainer();
        _endingStats.AddThemeConstantOverride("separation", 7);
        body.AddChild(_endingStats);
        body.AddChild(Line());
        var buttons = new HBoxContainer();
        body.AddChild(buttons);
        var export = Button("导出这次旅程  JSON", () => _game.ExportLog());
        export.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        buttons.AddChild(export);
        buttons.AddChild(Button("返回主菜单", () => _game.ReturnToMenu()));
        body.AddChild(Paragraph("自由探索没有标准速度。每一次绕路，也可能是一种发现。", 14));
    }

    private void BuildToast()
    {
        _toastPanel = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.AddChild(_toastPanel);
        _toastPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom);
        _toastPanel.OffsetLeft = -330; _toastPanel.OffsetRight = 330; _toastPanel.OffsetTop = -164; _toastPanel.OffsetBottom = -110;
        _toastText = Paragraph("", 17, Paper);
        _toastText.HorizontalAlignment = HorizontalAlignment.Center;
        _toastPanel.AddChild(_toastText);
    }

    private void Activate(string page)
    {
        _active = page;
        _overlay.Visible = page.Length > 0;
        _main.Visible = page == "main";
        _pageHost.Visible = page != "main";
        _pause.Visible = page == "pause";
        _settings.Visible = page == "settings";
        _map.Visible = page == "map";
        _journal.Visible = page == "journal";
        _ending.Visible = page == "ending";
        _hud.Visible = page.Length == 0;
        _toastPanel.Visible = _toastTime > 0;
        if (page.Length > 0)
        {
            var target = page == "main" ? _main : (Control)_pageHost;
            FocusFirst(target);
        }
        else GetViewport().GuiReleaseFocus();
    }

    private static bool FocusFirst(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Control control && !control.IsVisibleInTree()) continue;
            if (child is BaseButton button && !button.Disabled)
            {
                button.GrabFocus();
                return true;
            }
            if (FocusFirst(child)) return true;
        }
        return false;
    }

    public void ShowMainMenu()
    {
        _toastTime = 0;
        _continue.Disabled = !SaveStore.Exists;
        _continue.FocusMode = _continue.Disabled ? Control.FocusModeEnum.None : Control.FocusModeEnum.All;
        Activate("main");
    }

    public void ShowHud()
    {
        Activate("");
        Refresh();
    }

    public void ShowPause()
    {
        _game.Pause();
        Activate("pause");
    }

    public void ToggleMap()
    {
        if (_active == "map") { CloseOverlay(); return; }
        if (!_game.HasSession) return;
        _game.Pause();
        _mapView.Configure(_game.World, _game.State);
        _mapView.Refresh(new Vector2(_game.State.PlayerX, _game.State.PlayerY));
        _mapStats.Text = $"世界已显露  {ExploredPercent():0.0}%     ·     发现地点  {_game.State.Discovered.Count} / {_game.World.Landmarks.Count}     ·     未知仍在地图之外";
        Activate("map");
    }

    public void ToggleJournal()
    {
        if (_active == "journal") { CloseOverlay(); return; }
        if (!_game.HasSession) return;
        _game.Pause();
        RefreshJournal();
        Activate("journal");
    }

    private void RefreshJournal()
    {
        foreach (var child in _journalEntries.GetChildren())
        {
            _journalEntries.RemoveChild(child);
            child.QueueFree();
        }
        if (_game.State.Journal.Count == 0)
            _journalEntries.AddChild(Paragraph("纸页还是空白。向世界迈出第一步。", 18));
        foreach (var entry in _game.State.Journal.AsEnumerable().Reverse())
        {
            var row = new HBoxContainer();
            _journalEntries.AddChild(row);
            var time = Text(FormatTime(entry.Time), 14, Gold);
            time.CustomMinimumSize = new Vector2(80, 0);
            row.AddChild(time);
            var note = Paragraph(entry.Text, 17, Paper);
            note.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(note);
        }
        _hint.Visible = _game.HintsEnabled;
    }

    private void ShowSettings(string from)
    {
        _settingsFrom = from;
        _volume.SetValueNoSignal(_game.Volume);
        _hints.SetPressedNoSignal(_game.HintsEnabled);
        Activate("settings");
    }

    public void CloseOverlay()
    {
        if (_active == "settings")
        {
            if (_settingsFrom == "main") ShowMainMenu();
            else Activate("pause");
            return;
        }
        if (_active is "main" or "ending" or "") return;
        ShowHud();
        if (_game.HasSession) _game.Resume();
    }

    public void ShowEnding()
    {
        _game.Pause();
        foreach (var child in _endingStats.GetChildren())
        {
            _endingStats.RemoveChild(child);
            child.QueueFree();
        }
        var state = _game.State;
        StatRow("旅程时长", FormatTime(state.PlayedSeconds), "行走距离", $"{state.Distance / WorldData.TileSize:0} 格");
        StatRow("移动次数", $"{state.MoveCount}", "互动次数", $"{state.InteractionCount}");
        StatRow("发现地点", $"{state.Discovered.Count} / {_game.World.Landmarks.Count}", "地图探索", $"{ExploredPercent():0.0}%");
        StatRow("已发现区域", string.Join("、", state.Regions.Select(RegionName)), "机关回应", MechanismSummary());
        StatRow("探索效率", $"{state.Explored.Count / Math.Max(1, state.Distance / WorldData.TileSize):0.00} 格 / 移动格", "关键目标", "中央遗迹已开启");
        Activate("ending");
    }

    private void StatRow(string leftTitle, string leftValue, string rightTitle, string rightValue)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 24);
        _endingStats.AddChild(row);
        foreach (var item in new[] { (leftTitle, leftValue), (rightTitle, rightValue) })
        {
            var cell = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            cell.AddThemeConstantOverride("separation", 2);
            row.AddChild(cell);
            cell.AddChild(Text(item.Item1, 13, Gold));
            cell.AddChild(Paragraph(item.Item2, 17, Paper));
        }
    }

    private string MechanismSummary()
    {
        var names = new[] { ("forest", "林地"), ("dunes", "沙丘"), ("lake", "湖畔") };
        return string.Join(" · ", names.Select(pair => pair.Item2 + (_game.State.Activated.Contains(pair.Item1) ? " ✓" : " ○")));
    }

    private static string RegionName(string name) => name.ToLowerInvariant() switch
    {
        "meadow" => "草地", "forest" => "林地", "dunes" => "沙丘", "water" => "水域", "ruins" => "遗迹", "path" => "古道", _ => name
    };

    private double ExploredPercent() => 100.0 * _game.State.Explored.Count / (WorldData.Size * WorldData.Size);

    public void Refresh()
    {
        if (!_game.HasSession) return;
        _region.Text = _game.CurrentRegion;
        int signals = new[] { "forest", "dunes", "lake" }.Count(id => _game.State.Activated.Contains(id));
        _progress.Text = "遗迹信号  " + string.Join("  ", Enumerable.Range(0, 3).Select(i => i < signals ? "◆" : "◇"));
        _promptPanel.Visible = _game.InteractionPrompt.Length > 0;
        _prompt.Text = _game.InteractionPrompt;
    }

    public void Toast(string message, bool success = false)
    {
        _toastText.Text = message;
        _toastPanel.AddThemeStyleboxOverride("panel", Box(new Color("101b22", 0.98f), success ? Cyan : Gold, 8, 20, 12));
        _toastTime = Math.Clamp(3.2 + message.Length * 0.045, 3.2, 8);
        _toastPanel.Modulate = Colors.White;
        _toastPanel.Visible = true;
        if (_active == "journal") RefreshJournal();
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (_active == "settings" && _settingsFrom == "main" && input.IsActionPressed("pause"))
        {
            CloseOverlay();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (_toastTime <= 0) return;
        _toastTime -= delta;
        var color = Colors.White;
        color.A = (float)Math.Clamp(_toastTime / 0.4, 0, 1);
        _toastPanel.Modulate = color;
        if (_toastTime <= 0) _toastPanel.Visible = false;
    }

    private static string FormatTime(double seconds)
    {
        var time = TimeSpan.FromSeconds(seconds);
        return time.TotalHours >= 1 ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}" : $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";
    }
}
