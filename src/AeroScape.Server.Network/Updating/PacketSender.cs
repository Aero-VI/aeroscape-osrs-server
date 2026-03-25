using AeroScape.Server.Core.Entities;
using AeroScape.Server.Network.Protocol;
using AeroScape.Server.Network.Session;

namespace AeroScape.Server.Network.Updating;

/// <summary>
/// Utility for sending common outgoing packets to a player session.
/// Centralizes packet building logic used by handlers and the update cycle.
/// </summary>
public static class PacketSender
{
    public static async ValueTask SendMessage(PlayerSession session, ProtocolService protocol, string text, CancellationToken ct = default)
    {
        var def = protocol.GetOutgoingByName("SendMessage");
        if (def == null) return;
        var pkt = new PacketBuilder();
        pkt.WriteString(text);
        await session.SendPacketAsync(pkt.BuildVarByte(def.Opcode, session.OutgoingCipher), ct);
    }

    public static async ValueTask SendMapRegion(PlayerSession session, ProtocolService protocol, CancellationToken ct = default)
    {
        var def = protocol.GetOutgoingByName("MapRegion");
        if (def == null) return;
        var pkt = new PacketBuilder();
        pkt.WriteShortA(session.Player.Position.RegionX);
        pkt.WriteShort(session.Player.Position.RegionY);
        await session.SendPacketAsync(pkt.BuildVarShort(def.Opcode, session.OutgoingCipher), ct);
    }

    public static async ValueTask SendInventory(PlayerSession session, ProtocolService protocol, CancellationToken ct = default)
    {
        var def = protocol.GetOutgoingByName("SetItems");
        if (def == null) return;

        var pkt = new PacketBuilder();
        pkt.WriteShort(3214); // inventory interface
        pkt.WriteShort(session.Player.Inventory.Capacity);
        WriteContainer(pkt, session.Player.Inventory);
        await session.SendPacketAsync(pkt.BuildVarShort(def.Opcode, session.OutgoingCipher), ct);
    }

    public static async ValueTask SendEquipment(PlayerSession session, ProtocolService protocol, CancellationToken ct = default)
    {
        var def = protocol.GetOutgoingByName("SetItems");
        if (def == null) return;

        var pkt = new PacketBuilder();
        pkt.WriteShort(1688); // equipment interface
        pkt.WriteShort(session.Player.Equipment.Capacity);
        WriteContainer(pkt, session.Player.Equipment);
        await session.SendPacketAsync(pkt.BuildVarShort(def.Opcode, session.OutgoingCipher), ct);
    }

    public static async ValueTask SendSkill(PlayerSession session, ProtocolService protocol, int skillId, CancellationToken ct = default)
    {
        var def = protocol.GetOutgoingByName("SendSkill");
        if (def == null) return;

        var pkt = new PacketBuilder();
        pkt.WriteByte(skillId);
        pkt.WriteInt(session.Player.Skills.GetExperience(skillId));
        pkt.WriteByte(session.Player.Skills.GetLevel(skillId));
        await session.SendPacketAsync(pkt.Build(def.Opcode, session.OutgoingCipher), ct);
    }

    public static async ValueTask SendAllSkills(PlayerSession session, ProtocolService protocol, CancellationToken ct = default)
    {
        for (int i = 0; i < SkillSet.SkillCount; i++)
            await SendSkill(session, protocol, i, ct);
    }

    public static async ValueTask SendEnergy(PlayerSession session, ProtocolService protocol, CancellationToken ct = default)
    {
        var def = protocol.GetOutgoingByName("SendEnergy");
        if (def == null) return;

        var pkt = new PacketBuilder();
        pkt.WriteByte(session.Player.RunEnergy);
        await session.SendPacketAsync(pkt.Build(def.Opcode, session.OutgoingCipher), ct);
    }

    public static async ValueTask SendWeight(PlayerSession session, ProtocolService protocol, CancellationToken ct = default)
    {
        var def = protocol.GetOutgoingByName("SendWeight");
        if (def == null) return;

        // Calculate weight from inventory + equipment
        // TODO: Use item definitions for real weight values
        var pkt = new PacketBuilder();
        pkt.WriteShort(0); // weight in grams
        await session.SendPacketAsync(pkt.Build(def.Opcode, session.OutgoingCipher), ct);
    }

    public static async ValueTask SendConfig(PlayerSession session, ProtocolService protocol, int configId, int value, CancellationToken ct = default)
    {
        var def = protocol.GetOutgoingByName("SendConfig");
        if (def == null) return;

        var pkt = new PacketBuilder();
        pkt.WriteLEShort(configId);
        pkt.WriteInt(value);
        await session.SendPacketAsync(pkt.Build(def.Opcode, session.OutgoingCipher), ct);
    }

    public static async ValueTask SendSidebar(PlayerSession session, ProtocolService protocol, int tab, int interfaceId, CancellationToken ct = default)
    {
        var def = protocol.GetOutgoingByName("SetSidebar");
        if (def == null) return;

        var pkt = new PacketBuilder();
        pkt.WriteShort(interfaceId);
        pkt.WriteByteA(tab);
        await session.SendPacketAsync(pkt.Build(def.Opcode, session.OutgoingCipher), ct);
    }

    public static async ValueTask SendInterface(PlayerSession session, ProtocolService protocol, int interfaceId, CancellationToken ct = default)
    {
        var def = protocol.GetOutgoingByName("SetInterface");
        if (def == null) return;

        var pkt = new PacketBuilder();
        pkt.WriteByte(0); // window mode
        pkt.WriteShort(interfaceId);
        await session.SendPacketAsync(pkt.Build(def.Opcode, session.OutgoingCipher), ct);
    }

    public static async ValueTask SendLogout(PlayerSession session, ProtocolService protocol, CancellationToken ct = default)
    {
        var def = protocol.GetOutgoingByName("Logout");
        if (def == null) return;

        var pkt = new PacketBuilder();
        await session.SendPacketAsync(pkt.Build(def.Opcode, session.OutgoingCipher), ct);
    }

    public static async ValueTask SendSystemUpdate(PlayerSession session, ProtocolService protocol, int ticks, CancellationToken ct = default)
    {
        var def = protocol.GetOutgoingByName("SystemUpdate");
        if (def == null) return;

        var pkt = new PacketBuilder();
        pkt.WriteShort(ticks);
        await session.SendPacketAsync(pkt.Build(def.Opcode, session.OutgoingCipher), ct);
    }

    public static async ValueTask SendPlayerOption(PlayerSession session, ProtocolService protocol, string text, int slot, bool top, CancellationToken ct = default)
    {
        var def = protocol.GetOutgoingByName("SetPlayerOption");
        if (def == null) return;

        var pkt = new PacketBuilder();
        pkt.WriteString(text);
        pkt.WriteByte(slot);
        pkt.WriteByte(top ? 1 : 0);
        await session.SendPacketAsync(pkt.BuildVarByte(def.Opcode, session.OutgoingCipher), ct);
    }

    private static void WriteContainer(PacketBuilder pkt, ItemContainer container)
    {
        for (int i = 0; i < container.Capacity; i++)
        {
            var item = container.Get(i);
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
                pkt.WriteLEShortA(item.Id + 1); // +1 because 0 = empty
            }
            else
            {
                pkt.WriteByte(0);
                pkt.WriteLEShortA(0);
            }
        }
    }
}
