namespace AeroScape.Server.Core.Entities;

/// <summary>
/// Represents an active trade between two players.
/// </summary>
public sealed class TradeSession
{
    public Player Player1 { get; }
    public Player Player2 { get; }
    
    public ItemContainer Offer1 { get; } = new(28);
    public ItemContainer Offer2 { get; } = new(28);
    
    public bool Accepted1 { get; set; }
    public bool Accepted2 { get; set; }
    
    public TradeState State { get; set; } = TradeState.FirstScreen;

    public TradeSession(Player player1, Player player2)
    {
        Player1 = player1;
        Player2 = player2;
    }

    public Player GetPartner(Player player) =>
        player == Player1 ? Player2 : Player1;

    public ItemContainer GetOffer(Player player) =>
        player == Player1 ? Offer1 : Offer2;

    public ItemContainer GetPartnerOffer(Player player) =>
        player == Player1 ? Offer2 : Offer1;

    public bool GetAccepted(Player player) =>
        player == Player1 ? Accepted1 : Accepted2;

    public void SetAccepted(Player player, bool value)
    {
        if (player == Player1) Accepted1 = value;
        else Accepted2 = value;
    }

    public void ResetAccepted()
    {
        Accepted1 = false;
        Accepted2 = false;
    }

    /// <summary>
    /// Execute the trade — move items between players.
    /// </summary>
    public bool Execute()
    {
        // Check if both players have space
        int needed1 = Offer2.Count; // Player1 receives Offer2
        int needed2 = Offer1.Count; // Player2 receives Offer1

        // Count free slots after removing offered items
        int free1 = CountFreeAfterRemoval(Player1, Offer1);
        int free2 = CountFreeAfterRemoval(Player2, Offer2);

        if (free1 < needed1 || free2 < needed2)
            return false;

        // Remove offered items from player inventories
        RemoveOfferedItems(Player1, Offer1);
        RemoveOfferedItems(Player2, Offer2);

        // Add received items
        for (int i = 0; i < Offer2.Capacity; i++)
        {
            var item = Offer2.Get(i);
            if (item != null) Player1.Inventory.Add(item.Clone());
        }

        for (int i = 0; i < Offer1.Capacity; i++)
        {
            var item = Offer1.Get(i);
            if (item != null) Player2.Inventory.Add(item.Clone());
        }

        return true;
    }

    private static int CountFreeAfterRemoval(Player player, ItemContainer offered)
    {
        int freeSlots = 0;
        for (int i = 0; i < player.Inventory.Capacity; i++)
        {
            var item = player.Inventory.Get(i);
            if (item == null)
            {
                freeSlots++;
            }
            else
            {
                // Check if this item is in the offered container
                for (int j = 0; j < offered.Capacity; j++)
                {
                    var offeredItem = offered.Get(j);
                    if (offeredItem != null && offeredItem.Id == item.Id)
                    {
                        freeSlots++;
                        break;
                    }
                }
            }
        }
        return freeSlots;
    }

    private static void RemoveOfferedItems(Player player, ItemContainer offered)
    {
        for (int i = 0; i < offered.Capacity; i++)
        {
            var item = offered.Get(i);
            if (item == null) continue;
            
            // Find and remove from player inventory
            for (int j = 0; j < player.Inventory.Capacity; j++)
            {
                var invItem = player.Inventory.Get(j);
                if (invItem != null && invItem.Id == item.Id)
                {
                    player.Inventory.Remove(j);
                    break;
                }
            }
        }
    }
}

public enum TradeState
{
    FirstScreen,
    SecondScreen,
    Complete,
    Declined
}
