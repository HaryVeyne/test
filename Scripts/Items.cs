using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectUnknown;

public enum ItemKind { Weapon, Armor, Accessory, Consumable }
public enum WeaponKind { Sword, Bow, Staff }

public sealed record ItemDefinition(string Id, string Name, ItemKind Kind, string Rarity, string Description,
    int Attack = 0, int Defense = 0, int Health = 0, WeaponKind Weapon = WeaponKind.Sword);

public static class Items
{
    public static readonly Dictionary<string, ItemDefinition> All = new()
    {
        ["rust_sword"] = new("rust_sword", "旧铁剑", ItemKind.Weapon, "普通", "快速近战，可连续挥击。", Attack: 3),
        ["hunter_bow"] = new("hunter_bow", "猎弓", ItemKind.Weapon, "普通", "发射快速箭矢，适合中远距离。", Attack: 5, Weapon: WeaponKind.Bow),
        ["ember_staff"] = new("ember_staff", "余烬法杖", ItemKind.Weapon, "普通", "魔弹命中后爆炸，伤及附近敌人。", Attack: 7, Weapon: WeaponKind.Staff),
        ["iron_sword"] = new("iron_sword", "精铁剑", ItemKind.Weapon, "精良", "提高近战伤害。", Attack: 9),
        ["guardian_blade"] = new("guardian_blade", "守卫长剑", ItemKind.Weapon, "史诗", "古墓守卫的武器，同时强化生命。", Attack: 15, Health: 20),
        ["solar_staff"] = new("solar_staff", "日核法杖", ItemKind.Weapon, "史诗", "熔炉核心制成的强力爆炸法杖。", Attack: 20, Weapon: WeaponKind.Staff),
        ["linen_armor"] = new("linen_armor", "亚麻护衣", ItemKind.Armor, "普通", "轻便护衣，提供基础防护。", Defense: 2),
        ["scale_armor"] = new("scale_armor", "鳞片甲", ItemKind.Armor, "精良", "适合地下城战斗的护甲。", Defense: 6, Health: 10),
        ["frost_armor"] = new("frost_armor", "霜纹甲", ItemKind.Armor, "稀有", "雪山遗迹的重甲，增加生命和防御。", Defense: 9, Health: 30),
        ["copper_ring"] = new("copper_ring", "铜指环", ItemKind.Accessory, "精良", "增加攻击力。", Attack: 3),
        ["tide_charm"] = new("tide_charm", "潮汐护符", ItemKind.Accessory, "稀有", "增加生命、防御，技能冷却缩短15%。", Defense: 2, Health: 20),
        ["health_potion"] = new("health_potion", "治疗药水", ItemKind.Consumable, "普通", "恢复60点生命。")
    };

    public static ItemDefinition Get(string id) => All[id];

    public static void Add(GameState state, string id, int count)
    {
        var stack = state.Inventory.FirstOrDefault(item => item.Id == id);
        if (stack == null) state.Inventory.Add(new ItemStack(id, count));
        else stack.Count += count;
    }

    public static bool Remove(GameState state, string id, int count = 1)
    {
        var stack = state.Inventory.FirstOrDefault(item => item.Id == id);
        if (count < 1 || stack == null || stack.Count < count) return false;
        stack.Count -= count;
        if (stack.Count == 0) state.Inventory.Remove(stack);
        return true;
    }

    public static bool Equip(GameState state, string id, out string message)
    {
        if (!state.Inventory.Any(item => item.Id == id && item.Count > 0))
        {
            message = "背包中没有该物品。";
            return false;
        }
        var item = Get(id);
        if (item.Kind == ItemKind.Consumable)
        {
            message = "消耗品无法装备。";
            return false;
        }
        switch (item.Kind)
        {
            case ItemKind.Weapon: state.WeaponId = id; break;
            case ItemKind.Armor: state.ArmorId = id; break;
            case ItemKind.Accessory: state.AccessoryId = id; break;
        }
        state.Health = Math.Min(state.Health, Progression.Stats(state).MaxHealth);
        message = "已装备：" + item.Name;
        return true;
    }

    public static bool Unequip(GameState state, string slot, out string message)
    {
        string id = slot switch { "weapon" => state.WeaponId, "armor" => state.ArmorId, "accessory" => state.AccessoryId, _ => "" };
        if (id.Length == 0)
        {
            message = "该装备栏为空。";
            return false;
        }
        switch (slot)
        {
            case "weapon": state.WeaponId = ""; break;
            case "armor": state.ArmorId = ""; break;
            case "accessory": state.AccessoryId = ""; break;
        }
        state.Health = Math.Min(state.Health, Progression.Stats(state).MaxHealth);
        message = "已卸下：" + Get(id).Name;
        return true;
    }

    public static bool Use(GameState state, string id, out string message)
    {
        if (id != "health_potion" || !state.Inventory.Any(item => item.Id == id && item.Count > 0))
        {
            message = "该物品无法使用。";
            return false;
        }
        float maxHealth = Progression.Stats(state).MaxHealth;
        if (state.Health <= 0 || state.Health >= maxHealth)
        {
            message = state.Health <= 0 ? "复活后才能使用药水。" : "生命已满。";
            return false;
        }
        float restored = Math.Min(60, maxHealth - state.Health);
        state.Health += restored;
        Remove(state, id);
        message = $"恢复{restored:0}点生命。";
        return true;
    }
}
