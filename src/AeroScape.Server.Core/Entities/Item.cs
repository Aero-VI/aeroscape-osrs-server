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
    
    /// <summary>
    /// If true, items with the same ID will stack.
    /// Bank containers are always stacking; inventory stacking depends on item definitions.
    /// </summary>
    public bool AlwaysStack { get; }

    /// <summary>
    /// Optional callback to check if an item ID is stackable (used for inventory).
    /// If null and AlwaysStack is false, items never stack.
    /// </summary>
    public Func<int, bool>? StackChecker { get; set; }

    public ItemContainer(int capacity, bool alwaysStack = false)
    {
        _items = new Item?[capacity];
        AlwaysStack = alwaysStack;
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

    /// <summary>
    /// Finds a slot containing the given item ID, or -1 if not found.
    /// </summary>
    public int FindSlot(int itemId)
    {
        for (int i = 0; i < _items.Length; i++)
            if (_items[i] is { } item && item.Id == itemId) return i;
        return -1;
    }

    /// <summary>
    /// Adds an item, stacking if possible.
    /// </summary>
    public bool Add(Item item)
    {
        bool canStack = AlwaysStack || (StackChecker?.Invoke(item.Id) ?? false);

        if (canStack)
        {
            // Try to stack with existing
            int existing = FindSlot(item.Id);
            if (existing != -1)
            {
                _items[existing]!.Amount += item.Amount;
                return true;
            }
        }

        int slot = FreeSlot();
        if (slot == -1) return false;
        _items[slot] = item;
        return true;
    }

    /// <summary>
    /// Adds an item with amount, stacking if possible.
    /// </summary>
    public bool Add(int itemId, int amount)
    {
        return Add(new Item(itemId, amount));
    }

    /// <summary>
    /// Removes an amount of an item by slot.
    /// Returns the removed item, or null if the slot was empty.
    /// </summary>
    public Item? Remove(int slot)
    {
        if (slot < 0 || slot >= _items.Length) return null;
        var item = _items[slot];
        _items[slot] = null;
        return item;
    }

    /// <summary>
    /// Removes a specific amount from a slot (for stackables).
    /// Returns true if successful.
    /// </summary>
    public bool Remove(int slot, int amount)
    {
        if (slot < 0 || slot >= _items.Length) return false;
        var item = _items[slot];
        if (item == null || item.Amount < amount) return false;

        item.Amount -= amount;
        if (item.Amount <= 0)
            _items[slot] = null;
        return true;
    }

    /// <summary>
    /// Checks if the container has at least the given amount of the item.
    /// </summary>
    public bool Contains(int itemId, int amount = 1)
    {
        int total = 0;
        for (int i = 0; i < _items.Length; i++)
        {
            if (_items[i] is { } item && item.Id == itemId)
            {
                total += item.Amount;
                if (total >= amount) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Gets the total amount of an item across all slots.
    /// </summary>
    public int GetAmount(int itemId)
    {
        int total = 0;
        for (int i = 0; i < _items.Length; i++)
        {
            if (_items[i] is { } item && item.Id == itemId)
                total += item.Amount;
        }
        return total;
    }

    public void Swap(int from, int to)
    {
        if (from >= 0 && from < _items.Length && to >= 0 && to < _items.Length)
            (_items[from], _items[to]) = (_items[to], _items[from]);
    }

    public void Clear() => Array.Clear(_items);

    public IReadOnlyList<Item?> All => _items;

    public int Count => _items.Count(i => i is not null);

    public int FreeSlots
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _items.Length; i++)
                if (_items[i] is null) count++;
            return count;
        }
    }
}
