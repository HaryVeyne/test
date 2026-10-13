using System;
using System.Collections.Generic;

namespace ProjectUnknown;

public sealed record PlayerStats(float MaxHealth, int Attack, int Defense, float MoveSpeed, float CooldownScale);

public static class Progression
{
    public static readonly HashSet<string> ActiveSkills = new() { "cleave", "volley", "nova" };
    public static readonly HashSet<string> PassiveSkills = new() { "vitality", "power", "guard" };
    public static readonly Dictionary<string, string> SkillNames = new()
    {
        ["cleave"] = "回旋斩", ["volley"] = "三连射", ["nova"] = "寒霜爆发",
        ["vitality"] = "体魄", ["power"] = "强攻", ["guard"] = "坚甲"
    };

    public static PlayerStats Stats(GameState state)
    {
        float health = 100 + (state.Level - 1) * 14 + state.Passives.GetValueOrDefault("vitality") * 20;
        int attack = 10 + (state.Level - 1) * 2 + state.Passives.GetValueOrDefault("power") * 3;
        int defense = 1 + (state.Level - 1) / 3 + state.Passives.GetValueOrDefault("guard") * 2;
        foreach (string id in new[] { state.WeaponId, state.ArmorId, state.AccessoryId })
        {
            if (id.Length == 0) continue;
            var item = Items.Get(id);
            health += item.Health;
            attack += item.Attack;
            defense += item.Defense;
        }
        return new PlayerStats(health, attack, defense, 150, state.AccessoryId == "tide_charm" ? 0.85f : 1);
    }

    public static int RequiredXp(int level) => 80 + (level - 1) * 40;

    public static int GrantExperience(GameState state, int amount)
    {
        state.Experience += amount;
        int levels = 0;
        while (state.Experience >= RequiredXp(state.Level))
        {
            state.Experience -= RequiredXp(state.Level);
            state.Level++;
            state.SkillPoints++;
            if (state.Health > 0) state.Health = Math.Min(Stats(state).MaxHealth, state.Health + 30);
            levels++;
        }
        return levels;
    }

    public static bool Learn(GameState state, string id, out string message)
    {
        if (!ActiveSkills.Contains(id) && !PassiveSkills.Contains(id))
        {
            message = "没有该技能。";
            return false;
        }
        if (state.SkillPoints < 1)
        {
            message = "技能点不足。";
            return false;
        }
        if (ActiveSkills.Contains(id))
        {
            if (!state.LearnedSkills.Add(id))
            {
                message = "已学会该技能。";
                return false;
            }
            message = "已学会：" + SkillNames[id];
        }
        else
        {
            int rank = state.Passives.GetValueOrDefault(id);
            if (rank >= 3)
            {
                message = "该强化已满级。";
                return false;
            }
            state.Passives[id] = rank + 1;
            if (id == "vitality" && state.Health > 0) state.Health += 20;
            message = $"{SkillNames[id]}提升至{rank + 1}级。";
        }
        state.SkillPoints--;
        return true;
    }
}
