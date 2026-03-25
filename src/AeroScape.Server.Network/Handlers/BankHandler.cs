using AeroScape.Server.Core.Entities;
using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Protocol;
using AeroScape.Server.Network.Session;
using AeroScape.Server.Network.Updating;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Network.Handlers;

/// <summary>
/// Handles banking operations — opening the bank interface, depositing, withdrawing.
/// In 508, the bank interface is 5292 (bank) and 5063 (inventory while banking).
/// </summary>
public static class BankService
{
    // 508 bank interface IDs
    public const int BankInterface = 5292;
    public const int BankInventoryInterface = 5063;

    // Bank booth object IDs (common in 508)
    public static readonly int[] BankBoothObjectIds = [2213, 6084, 10083, 11402, 11758, 12798, 12799, 14367, 19230, 24914, 25808, 26972, 27663, 35647, 36786];

    public static bool IsBankBooth(int objectId) => Array.IndexOf(BankBoothObjectIds, objectId) >= 0;

    public static async ValueTask OpenBank(PlayerSession session, ProtocolService protocol, CancellationToken ct)
    {
        var player = session.Player;

        // Send bank inventory interface
        await PacketSender.SendSidebar(session, protocol, 3, BankInventoryInterface, ct);

        // Send bank interface
        await PacketSender.SendInterface(session, protocol, BankInterface, ct);

        // Send bank contents
        await SendBankItems(session, protocol, ct);

        // Send inventory for banking view
        await PacketSender.SendInventory(session, protocol, ct);

        await PacketSender.SendMessage(session, protocol, "Use your bank to store items safely.", ct);
    }

    public static async ValueTask SendBankItems(PlayerSession session, ProtocolService protocol, CancellationToken ct)
    {
        var def = protocol.GetOutgoingByName("SetItems");
        if (def == null) return;

        var pkt = new PacketBuilder();
        pkt.WriteShort(5382); // bank container interface
        pkt.WriteShort(session.Player.Bank.Capacity);

        for (int i = 0; i < session.Player.Bank.Capacity; i++)
        {
            var item = session.Player.Bank.Get(i);
            if (item != null)
            {
                if (item.Amount > 254)
                {
                    pkt.WriteByte(255);
                    pkt.WriteInt(item.Amount);
                }
                else
                {
                    pkt.WriteByte(item.Amount);
                }
                pkt.WriteLEShortA(item.Id + 1);
            }
            else
            {
                pkt.WriteByte(0);
                pkt.WriteLEShortA(0);
            }
        }

        await session.SendPacketAsync(pkt.BuildVarShort(def.Opcode, session.OutgoingCipher), ct);
    }

    /// <summary>
    /// Deposits an item from inventory to bank.
    /// </summary>
    public static bool DepositItem(Player player, int slot, int amount)
    {
        var item = player.Inventory.Get(slot);
        if (item == null) return false;

        int depositAmount = Math.Min(amount, item.Amount);
        int bankSlot = player.Bank.FreeSlot();
        if (bankSlot == -1) return false; // Bank full

        // Check if item already in bank (stack)
        for (int i = 0; i < player.Bank.Capacity; i++)
        {
            var bankItem = player.Bank.Get(i);
            if (bankItem != null && bankItem.Id == item.Id)
            {
                bankItem.Amount += depositAmount;
                if (item.Amount <= depositAmount)
                    player.Inventory.Remove(slot);
                else
                    item.Amount -= depositAmount;
                return true;
            }
        }

        // New bank slot
        player.Bank.Set(bankSlot, new Item(item.Id, depositAmount));
        if (item.Amount <= depositAmount)
            player.Inventory.Remove(slot);
        else
            item.Amount -= depositAmount;
        return true;
    }

    /// <summary>
    /// Withdraws an item from bank to inventory.
    /// </summary>
    public static bool WithdrawItem(Player player, int slot, int amount)
    {
        var item = player.Bank.Get(slot);
        if (item == null) return false;

        int withdrawAmount = Math.Min(amount, item.Amount);
        int invSlot = player.Inventory.FreeSlot();
        if (invSlot == -1) return false; // Inventory full

        player.Inventory.Set(invSlot, new Item(item.Id, withdrawAmount));
        if (item.Amount <= withdrawAmount)
            player.Bank.Remove(slot);
        else
            item.Amount -= withdrawAmount;
        return true;
    }
}
