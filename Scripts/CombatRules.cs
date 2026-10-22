using System;

namespace ProjectUnknown;

public enum DifficultyLevel { Relaxed, Standard, Hard, Expert }
public enum DamageKind { Slash, Pierce, Arcane, Fire, Frost, Environment }

public sealed record DifficultyProfile(string Name, float Health, float Attack, int Defense, float Speed)
{
    public string Description => $"敌人生命 ×{Health:0.##} · 攻击 ×{Attack:0.##} · 防御加成 {Defense:+0;-0;0} · 移速 ×{Speed:0.##}";
}

public static class CombatRules
{
    public static readonly DifficultyProfile[] Difficulties =
    {
        new("轻松", .8f, .75f, -1, .95f), new("标准", 1, 1, 0, 1),
        new("困难", 1.35f, 1.3f, 2, 1.08f), new("极难", 1.65f, 1.6f, 4, 1.12f)
    };

    public static DifficultyProfile Profile(DifficultyLevel level) => Difficulties[(int)level];
    public static string DamageName(DamageKind kind) => kind switch
    {
        DamageKind.Slash => "斩击", DamageKind.Pierce => "穿刺", DamageKind.Arcane => "奥术",
        DamageKind.Fire => "火焰", DamageKind.Frost => "寒霜", _ => "环境"
    };
    public static string EnemyName(string kind) => kind switch
    {
        "wolf" => "荒原狼", "guardian" => "持盾守卫", "archer" => "游荡弓手", "spore" => "孢子怪",
        "elite" => "重甲队长", "root_boss" => "古木守卫", "forge_boss" => "熔炉核心", _ => "拾荒者"
    };
    public static string Affinity(string enemy) => enemy switch
    {
        "guardian" or "elite" or "forge_boss" => "重甲",
        "spore" or "root_boss" => "植物", "wolf" => "野兽", _ => "轻甲"
    };
    public static float Multiplier(string enemy, DamageKind damage) => (Affinity(enemy), damage) switch
    {
        ("重甲", DamageKind.Slash) => .75f, ("重甲", DamageKind.Pierce) => .65f,
        ("重甲", DamageKind.Arcane) => 1.3f, ("重甲", DamageKind.Frost) => 1.1f,
        ("植物", DamageKind.Pierce) => .75f, ("植物", DamageKind.Fire) => 1.35f,
        ("植物", DamageKind.Frost) => .8f, ("野兽", DamageKind.Slash) => 1.25f,
        ("野兽", DamageKind.Frost) => .75f, ("轻甲", DamageKind.Pierce) => 1.2f, _ => 1
    };
    public static string AffinityHint(string enemy) => Affinity(enemy) switch
    {
        "重甲" => "弱：奥术 ×1.30\n抗：斩击 ×0.75 / 穿刺 ×0.65",
        "植物" => "弱：火焰 ×1.35\n抗：穿刺 ×0.75 / 寒霜 ×0.80",
        "野兽" => "弱：斩击 ×1.25\n抗：寒霜 ×0.75", _ => "弱：穿刺 ×1.20"
    };
    public static string WeaponHint(DamageKind damage) => damage switch
    {
        DamageKind.Slash => "对野兽更有效，重甲抵抗斩击。",
        DamageKind.Pierce => "对轻甲更有效，重甲和植物抵抗穿刺。",
        DamageKind.Arcane => "对重甲更有效。", DamageKind.Fire => "对植物更有效。",
        DamageKind.Frost => "对重甲略强，野兽和植物抵抗寒霜。", _ => ""
    };
}
