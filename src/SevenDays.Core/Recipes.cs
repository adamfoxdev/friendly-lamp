namespace SevenDays.Core;

public sealed record Recipe(ItemId Result, int Amount, params (ItemId Item, int Count)[] Cost)
{
    public bool CanCraft(Inventory inv) => Cost.All(c => inv.Has(c.Item, c.Count));

    public bool TryCraft(Inventory inv)
    {
        if (!CanCraft(inv)) return false;
        foreach (var (item, count) in Cost) inv.TryRemove(item, count);
        inv.Add(Result, Amount);
        return true;
    }

    public string CostText => string.Join(", ", Cost.Select(c => $"{c.Count} {Items.Get(c.Item).Name}"));
}

public static class Recipes
{
    public static readonly IReadOnlyList<Recipe> All = new[]
    {
        new Recipe(ItemId.Club, 1, (ItemId.Wood, 4)),
        new Recipe(ItemId.StoneAxe, 1, (ItemId.Wood, 2), (ItemId.Stone, 3), (ItemId.Cloth, 1)),
        new Recipe(ItemId.Spear, 1, (ItemId.Wood, 3), (ItemId.Stone, 2), (ItemId.Cloth, 1)),
        new Recipe(ItemId.Bandage, 1, (ItemId.Cloth, 2)),
        new Recipe(ItemId.WoodWall, 1, (ItemId.Wood, 4)),
        new Recipe(ItemId.StoneWall, 1, (ItemId.Stone, 6)),
        new Recipe(ItemId.WoodSpikes, 1, (ItemId.Wood, 5)),
    };
}
