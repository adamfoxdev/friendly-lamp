namespace SevenDays.Core;

public sealed class Inventory
{
    private readonly Dictionary<ItemId, int> _items = new();

    public IEnumerable<KeyValuePair<ItemId, int>> Entries =>
        _items.Where(kv => kv.Value > 0).OrderBy(kv => kv.Key);

    public int Count(ItemId id) => _items.GetValueOrDefault(id);

    public bool Has(ItemId id, int amount = 1) => Count(id) >= amount;

    public void Add(ItemId id, int amount = 1)
    {
        if (amount <= 0) return;
        _items[id] = Count(id) + amount;
    }

    public bool TryRemove(ItemId id, int amount = 1)
    {
        if (!Has(id, amount)) return false;
        _items[id] = Count(id) - amount;
        return true;
    }

    /// <summary>Best weapon damage in the bag (fists if none).</summary>
    public int BestDamage() =>
        Math.Max(Items.FistDamage, _items.Where(kv => kv.Value > 0).Select(kv => Items.Get(kv.Key).Damage).DefaultIfEmpty(0).Max());

    /// <summary>Best gathering bonus in the bag.</summary>
    public int BestGatherBonus() =>
        _items.Where(kv => kv.Value > 0).Select(kv => Items.Get(kv.Key).GatherBonus).DefaultIfEmpty(0).Max();
}
