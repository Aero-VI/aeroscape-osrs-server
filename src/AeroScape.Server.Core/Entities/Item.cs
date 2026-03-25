namespace AeroScape.Server.Core.Entities;

public sealed class Item
{
    public int Id { get; set; }
    public int Amount { get; set; }

    public Item(int id, int amount = 1)
    {
        Id = id;
        Amount = amount;
    }

    public Item Clone() => new(Id, Amount);
}

public sealed class ItemContainer
{
    private readonly Item?[] _items;
    public int Capacity => _items.Length;

    public ItemContainer(int capacity)
    {
        _items = new Item?[capacity];
    }

    public Item? Get(int slot) =>
        slot >= 0 && slot < _items.Length ? _items[slot] : null;

    public void Set(int slot, Item? item)
    {
        if (slot >= 0 && slot < _items.Length)
            _items[slot] = item;
    }

    public int FreeSlot()
    {
        for (int i = 0; i < _items.Length; i++)
            if (_items[i] is null) return i;
        return -1;
    }

    public bool Add(Item item)
    {
        int slot = FreeSlot();
        if (slot == -1) return false;
        _items[slot] = item;
        return true;
    }

    public Item? Remove(int slot)
    {
        var item = _items[slot];
        _items[slot] = null;
        return item;
    }

    public void Swap(int from, int to)
    {
        (_items[from], _items[to]) = (_items[to], _items[from]);
    }

    public void Clear() => Array.Clear(_items);

    public IReadOnlyList<Item?> All => _items;

    public int Count => _items.Count(i => i is not null);
}
