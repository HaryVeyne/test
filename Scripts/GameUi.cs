using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectUnknown;

public partial class GameUi : CanvasLayer
{
    private static readonly Color Ink = new("101b22");
    private static readonly Color Paper = new("e8e6d7");
    private static readonly Color Muted = new("9caeac");
    private static readonly Color Gold = new("d6b975");
    private static readonly Color Cyan = new("82c5c0");
    private static readonly string[] SkillIds = { "cleave", "volley", "nova" };
    private static readonly string[] SkillNames = { "回旋斩", "三连射", "寒霜爆发" };
    private Game _game = null!;
    private Control _root = null!, _hud = null!, _overlay = null!, _main = null!;
    private CenterContainer _pageHost = null!;
    private PanelContainer _pause = null!, _settings = null!, _map = null!, _journal = null!, _ending = null!;
    private PanelContainer _inventory = null!, _character = null!, _death = null!, _milestone = null!, _bossPanel = null!;
    private Label _region = null!, _progress = null!, _prompt = null!, _toastText = null!, _mapStats = null!;
    private Label _healthText = null!, _levelText = null!, _experienceText = null!, _dodgeText = null!, _bossName = null!;
    private Label _equipmentText = null!, _itemName = null!, _itemDetail = null!, _characterStats = null!, _skillPoints = null!;
    private Label _endingTitle = null!, _endingReward = null!, _milestoneTitle = null!, _milestoneReward = null!;
    private Label[] _skillText = null!;
    private ProgressBar _healthBar = null!, _experienceBar = null!, _bossBar = null!;
    private PanelContainer _promptPanel = null!, _toastPanel = null!;
    private Button _continue = null!, _hint = null!, _equip = null!, _use = null!, _drop = null!;
    private VBoxContainer _journalEntries = null!, _endingStats = null!, _inventoryEntries = null!, _skillEntries = null!;
    private HBoxContainer _travelButtons = null!;
    private CheckBox _hints = null!;
    private HSlider _volume = null!;
    private MapView _mapView = null!;
    private string _active = "main", _settingsFrom = "main", _selectedItem = "", _inventorySignature = "", _skillSignature = "";
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
        BuildInventory();
        BuildCharacter();
        BuildEnding();
        BuildDeath();
        BuildMilestone();
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
        theme.SetColor("font_disabled_color", "Button", new Color("71807d"));
        theme.SetColor("font_color", "CheckBox", Paper);
        theme.SetStylebox("normal", "Button", Box(new Color("24323a"), new Color("53635e"), 7, 16, 10));
        theme.SetStylebox("hover", "Button", Box(Gold, Gold, 7, 16, 10));
        theme.SetStylebox("pressed", "Button", Box(new Color("bba16a"), Gold, 7, 16, 10));
        theme.SetStylebox("disabled", "Button", Box(new Color("1c282e"), new Color("34423f"), 7, 16, 10));
        theme.SetStylebox("focus", "Button", Box(Colors.Transparent, Cyan, 7, 0, 0, 2));
        theme.SetStylebox("panel", "PanelContainer", Box(new Color("111d25", .97f), new Color("506058"), 12, 26, 20));
        theme.SetConstant("separation", "VBoxContainer", 10);
        theme.SetConstant("separation", "HBoxContainer", 14);
        theme.SetStylebox("slider", "HSlider", Box(new Color("344840"), new Color("344840"), 4, 0, 3));
        theme.SetStylebox("grabber_area", "HSlider", Box(Gold, Gold, 4, 0, 3));
        theme.SetStylebox("grabber_area_highlight", "HSlider", Box(Cyan, Cyan, 4, 0, 3));
        return theme;
    }

    private static StyleBoxFlat Box(Color fill, Color border, int radius = 6, int horizontal = 16, int vertical = 12, int borderSize = 1) => new()
    {
        BgColor = fill, BorderColor = border,
        BorderWidthLeft = borderSize, BorderWidthRight = borderSize, BorderWidthTop = borderSize, BorderWidthBottom = borderSize,
        CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
        ContentMarginLeft = horizontal, ContentMarginRight = horizontal, ContentMarginTop = vertical, ContentMarginBottom = vertical
    };

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
        var button = new Button { Text = label, CustomMinimumSize = new Vector2(0, 42), MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        button.Pressed += action;
        return button;
    }
    private static ColorRect Line() => new() { Color = new Color("40514b"), CustomMinimumSize = new Vector2(0, 1), MouseFilter = Control.MouseFilterEnum.Ignore };
    private static ProgressBar Bar(Color fill, float height)
    {
        var bar = new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(0, height), MouseFilter = Control.MouseFilterEnum.Ignore };
        bar.AddThemeStyleboxOverride("background", Box(new Color("263438"), new Color("42524d"), 3, 0, 0));
        bar.AddThemeStyleboxOverride("fill", Box(fill, fill, 3, 0, 0, 0));
        return bar;
    }
    private static void Clear(Node parent)
    {
        foreach (var child in parent.GetChildren()) { parent.RemoveChild(child); child.QueueFree(); }
    }

    private void BuildHud()
    {
        _hud = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.AddChild(_hud);
        Fill(_hud);
        var status = new PanelContainer { Position = new Vector2(24, 20), CustomMinimumSize = new Vector2(292, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        status.AddThemeStyleboxOverride("panel", Box(new Color("101b22", .9f), new Color("536052", .7f), 8, 16, 10));
        _hud.AddChild(status);
        var area = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        area.AddThemeConstantOverride("separation", 5);
        status.AddChild(area);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        area.AddChild(row);
        _levelText = Text("Lv. 1", 18, Gold); _levelText.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _healthText = Text("100 / 100", 18);
        row.AddChild(_levelText); row.AddChild(_healthText);
        _healthBar = Bar(new Color("bd645c"), 12); area.AddChild(_healthBar);
        _experienceText = Text("经验 0 / 80", 13, Muted); area.AddChild(_experienceText);
        _experienceBar = Bar(Gold, 5); area.AddChild(_experienceBar);
        _region = Text("草原", 15, Paper); area.AddChild(_region);

        var navigation = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        navigation.AddThemeStyleboxOverride("panel", Box(new Color("101b22", .86f), new Color("536052", .7f), 8, 15, 10));
        _hud.AddChild(navigation);
        navigation.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        navigation.OffsetLeft = -294; navigation.OffsetRight = -24; navigation.OffsetTop = 20;
        var right = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        right.AddThemeConstantOverride("separation", 4); navigation.AddChild(right);
        right.AddChild(Text("I 背包    C 角色    M 地图", 15));
        right.AddChild(Text("J 日志    Esc 暂停", 14, Muted));
        _progress = Text("遗迹封印 0 / 3", 14, Gold); right.AddChild(_progress);

        _bossPanel = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        _bossPanel.AddThemeStyleboxOverride("panel", Box(new Color("101b22", .94f), new Color("987260"), 7, 15, 8));
        _hud.AddChild(_bossPanel);
        _bossPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        _bossPanel.OffsetLeft = -208; _bossPanel.OffsetRight = 208; _bossPanel.OffsetTop = 20;
        var boss = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; boss.AddThemeConstantOverride("separation", 4);
        _bossPanel.AddChild(boss);
        _bossName = Text("", 17); _bossName.HorizontalAlignment = HorizontalAlignment.Center; boss.AddChild(_bossName);
        _bossBar = Bar(new Color("c75f5a"), 10); boss.AddChild(_bossBar);

        var hotbar = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        hotbar.AddThemeStyleboxOverride("panel", Box(new Color("101b22", .9f), new Color("536052", .7f), 8, 18, 10));
        _hud.AddChild(hotbar);
        hotbar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom);
        hotbar.OffsetLeft = -326; hotbar.OffsetRight = 326; hotbar.OffsetTop = -65; hotbar.OffsetBottom = -18;
        var skills = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; hotbar.AddChild(skills);
        _skillText = new Label[3];
        for (int i = 0; i < 3; i++) { _skillText[i] = Text($"{i + 1} {SkillNames[i]}", 15); _skillText[i].SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; skills.AddChild(_skillText[i]); }
        _dodgeText = Text("Space 闪避", 15, Cyan); skills.AddChild(_dodgeText);

        _promptPanel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _promptPanel.AddThemeStyleboxOverride("panel", Box(new Color("101b22", .92f), Gold, 7, 20, 8));
        _hud.AddChild(_promptPanel);
        _promptPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom);
        _promptPanel.OffsetLeft = -236; _promptPanel.OffsetRight = 236; _promptPanel.OffsetTop = -122; _promptPanel.OffsetBottom = -77;
        _prompt = Text("", 18); _prompt.HorizontalAlignment = HorizontalAlignment.Center; _promptPanel.AddChild(_prompt);
    }

    private void BuildOverlay()
    {
        _overlay = new Control(); _root.AddChild(_overlay); Fill(_overlay);
        var shade = new ColorRect { Color = new Color("071119", .76f) }; _overlay.AddChild(shade); Fill(shade);
        _pageHost = new CenterContainer(); _overlay.AddChild(_pageHost); Fill(_pageHost);
    }
    private PanelContainer Page(Vector2 minimum, out VBoxContainer body)
    {
        var panel = new PanelContainer { CustomMinimumSize = minimum, Visible = false }; _pageHost.AddChild(panel);
        body = new VBoxContainer(); panel.AddChild(body); return panel;
    }
    private void Header(VBoxContainer body, string title, string close)
    {
        var row = new HBoxContainer(); body.AddChild(row);
        var label = Text(title, 28); label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row.AddChild(label);
        if (close.Length > 0) row.AddChild(Button(close, CloseOverlay));
        body.AddChild(Line());
    }

    private void BuildMainMenu()
    {
        _main = new Control(); _overlay.AddChild(_main); Fill(_main);
        var margin = new MarginContainer(); _main.AddChild(margin); Fill(margin);
        margin.AddThemeConstantOverride("margin_left", 84); margin.AddThemeConstantOverride("margin_right", 84);
        margin.AddThemeConstantOverride("margin_top", 58); margin.AddThemeConstantOverride("margin_bottom", 36);
        var rows = new VBoxContainer(); margin.AddChild(rows);
        var columns = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill }; columns.AddThemeConstantOverride("separation", 60); rows.AddChild(columns);
        var titles = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center }; columns.AddChild(titles);
        titles.AddChild(Text("PROJECT", 60)); titles.AddChild(Text("UNKNOWN", 60)); titles.AddChild(Text("未知之境", 34, Gold));
        var choices = new VBoxContainer { CustomMinimumSize = new Vector2(328, 0), Alignment = BoxContainer.AlignmentMode.Center }; columns.AddChild(choices);
        choices.AddChild(Button("新游戏", () => _game.StartNew()));
        _continue = Button("继续游戏", () => _game.ContinueGame()); choices.AddChild(_continue);
        choices.AddChild(Button("设置", () => ShowSettings("main"))); choices.AddChild(Button("退出游戏", () => _game.QuitGame()));
        choices.AddChild(Paragraph("WASD 移动 · 鼠标左键攻击\nSpace 闪避 · E 互动", 14));
        var footer = new HBoxContainer(); rows.AddChild(footer);
        var version = Text("V0.2  ·  单人动作探索 RPG", 13, Muted); version.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; footer.AddChild(version);
        footer.AddChild(Text("F11  全屏 / 窗口", 13, Muted));
    }
    private void BuildPause()
    {
        _pause = Page(new Vector2(430, 0), out var body); Header(body, "暂停", "");
        body.AddChild(Button("继续游戏   Esc", CloseOverlay)); body.AddChild(Button("保存游戏", () => _game.SaveGame()));
        body.AddChild(Button("设置", () => ShowSettings("pause"))); body.AddChild(Button("导出探索记录 JSON", () => _game.ExportLog()));
        body.AddChild(Button("返回主菜单", () => _game.ReturnToMenu())); body.AddChild(Button("保存并退出", () => _game.QuitGame()));
    }
    private void BuildSettings()
    {
        _settings = Page(new Vector2(540, 0), out var body); Header(body, "设置", "");
        body.AddChild(Text("音量"));
        _volume = new HSlider { MinValue = 0, MaxValue = 1, Step = .01, Value = _game.Volume, CustomMinimumSize = new Vector2(0, 28) };
        _volume.ValueChanged += value => _game.SetVolume((float)value); body.AddChild(_volume);
        _hints = new CheckBox { Text = "允许渐进式解谜提示", ButtonPressed = _game.HintsEnabled };
        _hints.Toggled += value => _game.SetHints(value); body.AddChild(_hints);
        body.AddChild(Paragraph("在探索日志中主动请求提示。", 15)); body.AddChild(Line());
        body.AddChild(Button("切换全屏 / 窗口   F11", () => _game.ToggleFullscreen()));
        body.AddChild(Paragraph("设置自动保存。支持调整窗口大小。", 14)); body.AddChild(Button("返回   Esc", CloseOverlay));
    }
    private void BuildMap()
    {
        _map = Page(new Vector2(1050, 630), out var body); Header(body, "地图", "关闭   M / Esc");
        _mapStats = Text("", 14, Muted); body.AddChild(_mapStats);
        _mapView = new MapView { CustomMinimumSize = new Vector2(940, 388), SizeFlagsVertical = Control.SizeFlags.ExpandFill }; body.AddChild(_mapView);
        body.AddChild(Text("金色：地点  ·  青色：已激活  ·  浅色箭头：玩家  ·  黑色：未探索", 13, Muted));
        _travelButtons = new HBoxContainer(); body.AddChild(_travelButtons);
    }
    private void BuildJournal()
    {
        _journal = Page(new Vector2(900, 610), out var body); Header(body, "探索日志", "关闭   J / Esc");
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 380), SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; body.AddChild(scroll);
        _journalEntries = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; _journalEntries.AddThemeConstantOverride("separation", 16); scroll.AddChild(_journalEntries);
        body.AddChild(Line()); var footer = new HBoxContainer(); body.AddChild(footer);
        _hint = Button("请求解谜提示", () => _game.RequestHint()); _hint.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; footer.AddChild(_hint);
        footer.AddChild(Button("导出探索记录", () => _game.ExportLog()));
    }
    private void BuildInventory()
    {
        _inventory = Page(new Vector2(980, 620), out var body); Header(body, "背包", "关闭   I / Esc");
        _equipmentText = Paragraph("", 16, Cyan); body.AddChild(_equipmentText);
        var columns = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill }; body.AddChild(columns);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(338, 340), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; columns.AddChild(scroll);
        _inventoryEntries = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; _inventoryEntries.AddThemeConstantOverride("separation", 7); scroll.AddChild(_inventoryEntries);
        var details = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; columns.AddChild(details);
        _itemName = Text("", 24); details.AddChild(_itemName); _itemDetail = Paragraph("", 17, Paper); _itemDetail.SizeFlagsVertical = Control.SizeFlags.ExpandFill; details.AddChild(_itemDetail);
        var actions = new HBoxContainer(); details.AddChild(actions);
        _equip = Button("装备", () => { _game.EquipItem(_selectedItem); }); actions.AddChild(_equip);
        _use = Button("使用", () => { _game.UseItem(_selectedItem); }); actions.AddChild(_use);
        _drop = Button("丢弃 1 个", () => { _game.DropItem(_selectedItem); }); actions.AddChild(_drop);
        body.AddChild(Line()); var slots = new HBoxContainer(); body.AddChild(slots);
        foreach (var slot in new[] { ("weapon", "卸下武器"), ("armor", "卸下防具"), ("accessory", "卸下饰品") })
        {
            var button = Button(slot.Item2, () => { _game.UnequipItem(slot.Item1); }); button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; slots.AddChild(button);
        }
        body.AddChild(Text("装备保留在背包中。丢弃后可在原地按 E 拾取。", 13, Muted));
    }
    private void BuildCharacter()
    {
        _character = Page(new Vector2(980, 620), out var body); Header(body, "角色与技能", "关闭   C / Esc");
        _characterStats = Paragraph("", 17, Paper); body.AddChild(_characterStats);
        _skillPoints = Text("", 18, Gold); body.AddChild(_skillPoints); body.AddChild(Line());
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 360), SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; body.AddChild(scroll);
        _skillEntries = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; _skillEntries.AddThemeConstantOverride("separation", 12); scroll.AddChild(_skillEntries);
        body.AddChild(Text("升级获得技能点。主动技能使用数字键，被动强化立即生效。", 13, Muted));
    }
    private void BuildEnding()
    {
        _ending = Page(new Vector2(870, 590), out var body); _endingTitle = Text("主线完成", 30); body.AddChild(_endingTitle);
        _endingReward = Paragraph("", 17, Cyan); body.AddChild(_endingReward); body.AddChild(Line());
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 280), SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; body.AddChild(scroll);
        _endingStats = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; _endingStats.AddThemeConstantOverride("separation", 9); scroll.AddChild(_endingStats); body.AddChild(Line());
        body.AddChild(Button("继续探索", () => _game.ResumeAfterResult()));
        var buttons = new HBoxContainer(); body.AddChild(buttons);
        var export = Button("导出探索记录 JSON", () => _game.ExportLog()); export.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; buttons.AddChild(export);
        buttons.AddChild(Button("返回主菜单", () => _game.ReturnToMenu()));
    }
    private void BuildDeath()
    {
        _death = Page(new Vector2(490, 0), out var body); Header(body, "角色死亡", "");
        body.AddChild(Paragraph("在最近激活的检查点复活，恢复全部生命。保留装备、经验和已完成的进度。", 17, Paper));
        body.AddChild(Button("在检查点复活", () => _game.Respawn())); body.AddChild(Button("返回主菜单", () => _game.ReturnToMenu()));
    }
    private void BuildMilestone()
    {
        _milestone = Page(new Vector2(610, 0), out var body);
        _milestoneTitle = Text("", 28); body.AddChild(_milestoneTitle); body.AddChild(Line());
        _milestoneReward = Paragraph("", 18, Paper); body.AddChild(_milestoneReward);
        body.AddChild(Button("继续游戏", () => _game.ResumeAfterResult()));
    }
    private void BuildToast()
    {
        _toastPanel = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore }; _root.AddChild(_toastPanel);
        _toastPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom);
        _toastPanel.OffsetLeft = -340; _toastPanel.OffsetRight = 340; _toastPanel.OffsetTop = -191; _toastPanel.OffsetBottom = -133;
        _toastText = Paragraph("", 17, Paper); _toastText.HorizontalAlignment = HorizontalAlignment.Center; _toastPanel.AddChild(_toastText);
    }

    private void Activate(string page)
    {
        _active = page; _overlay.Visible = page.Length > 0; _main.Visible = page == "main"; _pageHost.Visible = page != "main";
        _pause.Visible = page == "pause"; _settings.Visible = page == "settings"; _map.Visible = page == "map"; _journal.Visible = page == "journal";
        _inventory.Visible = page == "inventory"; _character.Visible = page == "character"; _ending.Visible = page == "ending";
        _death.Visible = page == "death"; _milestone.Visible = page == "milestone"; _hud.Visible = page.Length == 0; _toastPanel.Visible = _toastTime > 0;
        PositionToast();
        if (page.Length > 0) FocusFirst(page == "main" ? _main : _pageHost); else GetViewport().GuiReleaseFocus();
    }
    private static bool FocusFirst(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Control control && !control.IsVisibleInTree()) continue;
            if (child is BaseButton button && !button.Disabled) { button.GrabFocus(); return true; }
            if (FocusFirst(child)) return true;
        }
        return false;
    }
    public void ShowMainMenu()
    {
        _toastTime = 0; _continue.Disabled = !SaveStore.Exists;
        _continue.FocusMode = _continue.Disabled ? Control.FocusModeEnum.None : Control.FocusModeEnum.All; Activate("main");
    }
    public void ShowHud() { Activate(""); Refresh(); }
    public void ShowPause() { _game.Pause(); Activate("pause"); }
    public void ToggleMap()
    {
        if (_active == "map") { CloseOverlay(); return; }
        if (!_game.HasSession || _active == "death") return;
        _game.Pause(); _mapView.Configure(_game.World, _game.State); _mapView.Refresh(_game.Wanderer.Position);
        string area = _game.World.Area switch { "crypt" => "林地墓室", "forge" => "熔炉", _ => "地表" };
        _mapStats.Text = $"{area}  ·  已探索 {ExploredPercent():0.0}%  ·  已发现地点 {_game.World.Landmarks.Count(site => _game.State.Discovered.Contains(site.Id))} / {_game.World.Landmarks.Count}";
        Clear(_travelButtons); _travelButtons.AddChild(Text("快速旅行", 15, Gold));
        foreach (string id in _game.State.Checkpoints.OrderBy(value => value))
        {
            string checkpointId = id; var button = Button(CheckpointName(id), () => _game.TravelTo(checkpointId)); button.AddThemeFontSizeOverride("font_size", 14);
            button.Disabled = _game.World.Area != "world" || _game.Combat.HasThreatNearby; _travelButtons.AddChild(button);
        }
        if (_game.World.Area != "world") _travelButtons.AddChild(Text("地下城中不可用", 14, Muted));
        else if (_game.Combat.HasThreatNearby) _travelButtons.AddChild(Text("附近有敌人", 14, Muted));
        Activate("map");
    }
    public void ToggleJournal()
    {
        if (_active == "journal") { CloseOverlay(); return; }
        if (!_game.HasSession || _active == "death") return;
        _game.Pause(); RefreshJournal(); Activate("journal");
    }
    public void ToggleInventory()
    {
        if (_active == "inventory") { CloseOverlay(); return; }
        if (!_game.HasSession || _active == "death") return;
        _game.Pause(); RefreshInventory(); Activate("inventory");
    }
    public void ToggleCharacter()
    {
        if (_active == "character") { CloseOverlay(); return; }
        if (!_game.HasSession || _active == "death") return;
        _game.Pause(); RefreshCharacter(); Activate("character");
    }
    private void RefreshJournal()
    {
        Clear(_journalEntries);
        if (_game.State.Journal.Count == 0) _journalEntries.AddChild(Paragraph("暂无记录。", 18));
        foreach (var entry in _game.State.Journal.AsEnumerable().Reverse())
        {
            var row = new HBoxContainer(); _journalEntries.AddChild(row);
            var time = Text(FormatTime(entry.Time), 14, Gold); time.CustomMinimumSize = new Vector2(80, 0); row.AddChild(time);
            var note = Paragraph(entry.Text, 17, Paper); note.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row.AddChild(note);
        }
        _hint.Visible = _game.HintsEnabled;
    }
    private void RefreshInventory()
    {
        var state = _game.State;
        _inventorySignature = InventorySignature();
        _equipmentText.Text = $"武器：{ItemName(state.WeaponId)}    防具：{ItemName(state.ArmorId)}    饰品：{ItemName(state.AccessoryId)}";
        Clear(_inventoryEntries);
        if (!state.Inventory.Any(item => item.Id == _selectedItem)) _selectedItem = state.Inventory.FirstOrDefault()?.Id ?? "";
        foreach (var item in state.Inventory)
        {
            string id = item.Id; var definition = Items.Get(id); bool equipped = IsEquipped(id);
            var button = Button($"{definition.Name}  ×{item.Count}" + (equipped ? "  [已装备]" : $"  {definition.Rarity}"), () => { _selectedItem = id; RefreshItemDetails(); });
            button.AddThemeFontSizeOverride("font_size", 15); button.Alignment = HorizontalAlignment.Left;
            button.AddThemeColorOverride("font_color", RarityColor(definition.Rarity)); _inventoryEntries.AddChild(button);
        }
        if (state.Inventory.Count == 0) _inventoryEntries.AddChild(Text("背包为空", 17, Muted));
        RefreshItemDetails();
    }
    private void RefreshItemDetails()
    {
        bool selected = _selectedItem.Length > 0;
        _equip.Visible = false; _use.Visible = false; _drop.Visible = selected;
        if (!selected) { _itemName.Text = "选择物品"; _itemDetail.Text = ""; return; }
        var item = Items.Get(_selectedItem); var stats = _game.Stats;
        _itemName.Text = item.Name; _itemName.AddThemeColorOverride("font_color", RarityColor(item.Rarity));
        string kind = item.Kind switch { ItemKind.Weapon => item.Weapon switch { WeaponKind.Bow => "弓", WeaponKind.Staff => "法杖", _ => "剑" }, ItemKind.Armor => "防具", ItemKind.Accessory => "饰品", _ => "消耗品" };
        string attributes = "";
        if (item.Attack != 0) attributes += $"\n攻击 +{item.Attack}";
        if (item.Defense != 0) attributes += $"\n防御 +{item.Defense}";
        if (item.Health != 0) attributes += $"\n最大生命 +{item.Health}";
        _itemDetail.Text = $"{item.Rarity} · {kind}{attributes}\n\n{item.Description}\n\n当前角色\n攻击 {stats.Attack} · 防御 {stats.Defense}\n最大生命 {stats.MaxHealth:0}";
        _equip.Visible = item.Kind != ItemKind.Consumable; _equip.Disabled = IsEquipped(item.Id); _equip.Text = IsEquipped(item.Id) ? "已装备" : "装备";
        _use.Visible = item.Kind == ItemKind.Consumable; _use.Disabled = item.Id == "health_potion" && _game.State.Health >= stats.MaxHealth;
        _drop.Disabled = IsEquipped(item.Id); _drop.TooltipText = IsEquipped(item.Id) ? "先卸下装备再丢弃。" : "将一个物品放在玩家脚下。";
    }
    private bool IsEquipped(string id) => id.Length > 0 && (id == _game.State.WeaponId || id == _game.State.ArmorId || id == _game.State.AccessoryId);
    private static string ItemName(string id) => id.Length == 0 ? "无" : Items.Get(id).Name;
    private string InventorySignature() => string.Join("|", _game.State.Inventory.Select(item => $"{item.Id}:{item.Count}")) + $"/{_game.State.WeaponId}/{_game.State.ArmorId}/{_game.State.AccessoryId}/{_game.State.Health:0}/{_game.State.Level}";
    private static Color RarityColor(string rarity) => rarity switch { "精良" => Cyan, "稀有" => new Color("91afd9"), "史诗" => new Color("c3a0df"), _ => Paper };
    private void RefreshCharacter()
    {
        var state = _game.State; var stats = _game.Stats;
        _skillSignature = SkillSignature();
        _characterStats.Text = $"等级 {state.Level}    经验 {state.Experience} / {Progression.RequiredXp(state.Level)}\n生命 {state.Health:0} / {stats.MaxHealth:0}    攻击 {stats.Attack}    防御 {stats.Defense}    移动速度 {stats.MoveSpeed:0}";
        _skillPoints.Text = $"可用技能点：{state.SkillPoints}"; Clear(_skillEntries);
        AddSkillRow("cleave", "1 · 回旋斩", $"近身范围攻击，造成 2 倍攻击伤害并击退。冷却 {6 * stats.CooldownScale:0.#} 秒。", false);
        AddSkillRow("volley", "2 · 三连射", $"向瞄准方向发射三支穿透箭。冷却 {8 * stats.CooldownScale:0.#} 秒。", false);
        AddSkillRow("nova", "3 · 寒霜爆发", $"鼠标位置范围攻击，减速 3 秒。施法范围 250，冷却 {10 * stats.CooldownScale:0.#} 秒。", false);
        AddSkillRow("vitality", "体魄", "每级最大生命 +20，最多 3 级。", true);
        AddSkillRow("power", "强攻", "每级攻击 +3，最多 3 级。", true);
        AddSkillRow("guard", "坚甲", "每级防御 +2，最多 3 级。", true);
    }
    private void AddSkillRow(string id, string name, string description, bool passive)
    {
        var state = _game.State; int rank = passive ? state.Passives.GetValueOrDefault(id) : state.LearnedSkills.Contains(id) ? 1 : 0;
        var row = new HBoxContainer(); _skillEntries.AddChild(row);
        var labels = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; labels.AddThemeConstantOverride("separation", 3); row.AddChild(labels);
        labels.AddChild(Text(name + (passive ? $"  {rank} / 3" : rank == 1 ? "  [已学会]" : ""), 17, rank > 0 ? Cyan : Paper));
        labels.AddChild(Paragraph(description, 14));
        bool max = rank >= (passive ? 3 : 1); var button = Button(max ? "已掌握" : passive ? "强化 · 1 点" : "学习 · 1 点", () => { _game.LearnSkill(id); });
        button.CustomMinimumSize = new Vector2(145, 42); button.Disabled = max || state.SkillPoints <= 0; row.AddChild(button);
    }
    private string SkillSignature() => $"{_game.State.Level}/{_game.State.Experience}/{_game.State.SkillPoints}/{_game.State.Health:0}/{_game.State.WeaponId}/{_game.State.ArmorId}/{_game.State.AccessoryId}/" + string.Join(",", _game.State.LearnedSkills.OrderBy(id => id)) + "/" + string.Join(",", _game.State.Passives.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}"));
    private void ShowSettings(string from)
    {
        _settingsFrom = from; _volume.SetValueNoSignal(_game.Volume); _hints.SetPressedNoSignal(_game.HintsEnabled); Activate("settings");
    }
    public void CloseOverlay()
    {
        if (_active == "settings") { if (_settingsFrom == "main") ShowMainMenu(); else Activate("pause"); return; }
        if (_active is "main" or "death" or "ending" or "milestone" or "") return;
        ShowHud(); if (_game.HasSession) _game.Resume();
    }
    public void ShowDeath() { _game.Pause(); _toastTime = 0; Activate("death"); }
    public void ShowMilestone(string title, string reward)
    {
        _game.Pause(); _milestoneTitle.Text = title; _milestoneReward.Text = reward; _toastTime = 0; Activate("milestone");
    }
    public void ShowEnding()
    {
        _game.Pause(); Clear(_endingStats); var state = _game.State;
        _endingTitle.Text = state.RpgCompleted ? "主线完成" : "中央遗迹已开启";
        _endingReward.Text = state.RpgCompleted ? "中央遗迹、林地墓室与熔炉已完成。首领奖励：守卫长剑、日核法杖。可继续探索。" : "遗迹奖励：潮汐护符、160 经验、2 技能点。继续探索地下城，击败两名首领。";
        StatRow("游玩时间", FormatTime(state.PlayedSeconds), "角色等级", state.Level.ToString());
        StatRow("击败敌人", state.EnemyKills.ToString(), "死亡次数", state.Deaths.ToString());
        StatRow("首领", $"{state.BossesDefeated.Count} / 2", "打开宝箱", state.OpenedChests.Count.ToString());
        StatRow("地表探索", $"{100.0 * state.Explored.Count / (WorldData.Size * WorldData.Size):0.0}%", "行走距离", $"{state.Distance / WorldData.TileSize:0} 格");
        StatRow("互动次数", state.InteractionCount.ToString(), "遗迹封印", MechanismSummary());
        StatRow("已访问区域", string.Join("、", state.Regions.Select(RegionName)), "累计伤害 / 承伤", $"{state.DamageDealt} / {state.DamageTaken}");
        Activate("ending");
    }
    private void StatRow(string leftTitle, string leftValue, string rightTitle, string rightValue)
    {
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 24); _endingStats.AddChild(row);
        foreach (var item in new[] { (leftTitle, leftValue), (rightTitle, rightValue) })
        {
            var cell = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; cell.AddThemeConstantOverride("separation", 1); row.AddChild(cell);
            cell.AddChild(Text(item.Item1, 13, Gold)); cell.AddChild(Paragraph(item.Item2, 16, Paper));
        }
    }
    private string MechanismSummary() => string.Join(" · ", new[] { ("forest", "符文"), ("dunes", "古镜"), ("lake", "湖心") }.Select(pair => pair.Item2 + (_game.State.Activated.Contains(pair.Item1) ? " ✓" : " ○")));
    private static string RegionName(string name) => name.ToLowerInvariant() switch
    {
        "meadow" => "草原", "forest" => "森林", "dunes" => "荒漠", "water" => "湖泊", "ruins" => "遗迹", "path" => "古道", "wetland" => "湿地", "snow" => "雪山", "crypt" => "墓穴", "forge" => "工坊", _ => name
    };
    private static string CheckpointName(string id) => id switch { "camp" => "旧营地", "forest_camp" => "林地营地", "dunes_camp" => "沙丘营地", "wetland_camp" => "湿地营地", "snow_camp" => "雪山营地", _ => id };
    private double ExploredPercent() => 100.0 * _game.State.ExploredFor(_game.World.Area).Count / (_game.World.Width * _game.World.Height);
    public void Refresh()
    {
        if (!_game.HasSession) return;
        RefreshHud();
        if (_active == "inventory" && _inventorySignature != InventorySignature()) RefreshInventory();
        if (_active == "character" && _skillSignature != SkillSignature()) RefreshCharacter();
        if (_active == "map") { _mapView.Configure(_game.World, _game.State); _mapView.Refresh(_game.Wanderer.Position); }
    }
    private void RefreshHud()
    {
        var state = _game.State; var stats = _game.Stats;
        _region.Text = _game.CurrentRegion; _levelText.Text = $"Lv. {state.Level}"; _healthText.Text = $"{Math.Max(0, state.Health):0} / {stats.MaxHealth:0}";
        _healthBar.MaxValue = stats.MaxHealth; _healthBar.Value = state.Health;
        int xp = Progression.RequiredXp(state.Level); _experienceBar.MaxValue = xp; _experienceBar.Value = state.Experience; _experienceText.Text = $"经验 {state.Experience} / {xp}";
        int signals = new[] { "forest", "dunes", "lake" }.Count(id => state.Activated.Contains(id)); _progress.Text = $"遗迹封印 {signals} / 3  ·  首领 {state.BossesDefeated.Count} / 2";
        _promptPanel.Visible = _game.InteractionPrompt.Length > 0; _prompt.Text = _game.InteractionPrompt;
        for (int i = 0; i < 3; i++)
        {
            bool learned = state.LearnedSkills.Contains(SkillIds[i]); float cooldown = _game.Combat.SkillCooldowns[i];
            _skillText[i].Text = $"{i + 1} {SkillNames[i]}" + (!learned ? " 未学" : cooldown > 0 ? $" {cooldown:0.0}s" : " 就绪");
            _skillText[i].AddThemeColorOverride("font_color", !learned ? Muted : cooldown > 0 ? Gold : Paper);
        }
        float dodge = _game.Wanderer.DodgeCooldown; _dodgeText.Text = dodge > 0 ? $"闪避 {dodge:0.0}s" : "Space 闪避";
        var boss = _game.Combat.Enemies.FirstOrDefault(enemy => enemy.IsBoss && enemy.IsAlive && _game.Dungeons?.BossAccessible != false && enemy.Position.DistanceSquaredTo(_game.Wanderer.Position) < 800 * 800);
        _bossPanel.Visible = boss != null;
        if (boss != null)
        {
            string name = boss.Kind == "root_boss" ? "古木守卫" : "熔炉监工";
            _bossName.Text = $"{name}   {boss.Health} / {boss.MaxHealth}"; _bossBar.MaxValue = boss.MaxHealth; _bossBar.Value = boss.Health;
        }
    }
    private void PositionToast()
    {
        float overlayTop = _toastText.Text.Contains('\n') || _toastText.Text.Length > 80 ? -92 : -54;
        _toastPanel.OffsetTop = _active.Length == 0 ? -191 : overlayTop;
        _toastPanel.OffsetBottom = _active.Length == 0 ? -133 : -6;
    }
    public void Toast(string message, bool success = false)
    {
        _toastText.Text = message; PositionToast(); _toastPanel.AddThemeStyleboxOverride("panel", Box(new Color("101b22", .98f), success ? Cyan : Gold, 8, 20, _active.Length == 0 ? 12 : 6));
        _toastTime = Math.Clamp(2.2 + message.Length * .045, 2.2, 8); _toastPanel.Modulate = Colors.White; _toastPanel.Visible = true;
        if (_active == "journal") RefreshJournal();
    }
    public override void _UnhandledInput(InputEvent input)
    {
        if (_active == "settings" && _settingsFrom == "main" && input.IsActionPressed("pause")) { CloseOverlay(); GetViewport().SetInputAsHandled(); }
    }
    public override void _Process(double delta)
    {
        if (_game.HasSession && _active.Length == 0) RefreshHud();
        if (_toastTime <= 0) return;
        _toastTime -= delta; var color = Colors.White; color.A = (float)Math.Clamp(_toastTime / .4, 0, 1); _toastPanel.Modulate = color;
        if (_toastTime <= 0) _toastPanel.Visible = false;
    }
    private static string FormatTime(double seconds)
    {
        var time = TimeSpan.FromSeconds(seconds);
        return time.TotalHours >= 1 ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}" : $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";
    }
}
