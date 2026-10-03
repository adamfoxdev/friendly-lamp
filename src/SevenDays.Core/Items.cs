namespace SevenDays.Core;

public enum ItemId
{
    Wood, Stone, Cloth,
    CannedFood, WaterBottle, Bandage,
    Club, StoneAxe, Spear,
    WoodWall, StoneWall, WoodSpikes,
}

public sealed record ItemDef(
    ItemId Id,
    string Name,
    int Damage = 0,
    int GatherBonus = 0,
    int Food = 0,
    int Water = 0,
    int Heal = 0,
    TileKind? Places = null)
{
    public bool IsConsumable => Food > 0 || Water > 0 || Heal > 0;
}

public static class Items
{
    public const int FistDamage = 4;

    private static readonly Dictionary<ItemId, ItemDef> Defs = new ItemDef[]
    {
        new(ItemId.Wood, "Wood"),
        new(ItemId.Stone, "Stone"),
        new(ItemId.Cloth, "Cloth"),
        new(ItemId.CannedFood, "Canned Food", Food: 35),
        new(ItemId.WaterBottle, "Bottled Water", Water: 40),
        new(ItemId.Bandage, "Bandage", Heal: 25),
        new(ItemId.Club, "Wooden Club", Damage: 10),
        new(ItemId.StoneAxe, "Stone Axe", Damage: 8, GatherBonus: 1),
        new(ItemId.Spear, "Stone Spear", Damage: 15),
        new(ItemId.WoodWall, "Wood Wall", Places: TileKind.WoodWall),
        new(ItemId.StoneWall, "Stone Wall", Places: TileKind.StoneWall),
        new(ItemId.WoodSpikes, "Wood Spikes", Places: TileKind.WoodSpikes),
    }.ToDictionary(d => d.Id);

    public static ItemDef Get(ItemId id) => Defs[id];
}
