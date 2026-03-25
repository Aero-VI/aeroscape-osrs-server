using AeroScape.Server.Core.Entities;
using AeroScape.Server.Core.Game;
using AeroScape.Server.Network.Protocol;
using AeroScape.Server.Network.Session;

namespace AeroScape.Server.Network.Updating;

/// <summary>
/// Builds the NPC update packet (opcode 65, var-short) for the 508 protocol.
/// </summary>
public static class NpcUpdatePacket
{
    // NPC update flag masks (508)
    private const int FlagAnimation     = 0x10;
    private const int FlagHit           = 0x8;
    private const int FlagGraphic       = 0x80;
    private const int FlagFaceEntity    = 0x20;
    private const int FlagForcedChat    = 0x1;
    private const int FlagFaceCoord     = 0x4;
    private const int FlagTransform     = 0x2;
    private const int FlagHit2          = 0x40;

    public static ReadOnlyMemory<byte> Build(PlayerSession session, GameWorld world, ProtocolService protocol)
    {
        var player = session.Player;
        var pkt = new PacketBuilder(2048);
        var blockData = new PacketBuilder(4096);

        pkt.InitBitAccess();

        // --- Update existing local NPCs ---
        pkt.WriteBits(8, player.LocalNpcs.Count);

        for (int i = player.LocalNpcs.Count - 1; i >= 0; i--)
        {
            var npc = player.LocalNpcs[i];

            if (!npc.IsActive || !npc.Position.WithinDistance(player.Position))
            {
                // Remove
                pkt.WriteBits(1, 1);
                pkt.WriteBits(2, 3);
                player.LocalNpcs.RemoveAt(i);
            }
            else if (npc.WalkDirection != -1)
            {
                // Walking
                pkt.WriteBits(1, 1);
                pkt.WriteBits(2, 1);
                pkt.WriteBits(3, npc.WalkDirection);
                pkt.WriteBits(1, npc.UpdateRequired ? 1 : 0);
                if (npc.UpdateRequired)
                    AppendUpdateBlock(blockData, npc);
            }
            else if (npc.UpdateRequired)
            {
                pkt.WriteBits(1, 1);
                pkt.WriteBits(2, 0);
                AppendUpdateBlock(blockData, npc);
            }
            else
            {
                pkt.WriteBits(1, 0);
            }
        }

        // --- Add new local NPCs ---
        foreach (var npc in world.GetActiveNpcs())
        {
            if (player.LocalNpcs.Count >= 255) break;
            if (player.LocalNpcs.Contains(npc)) continue;
            if (!npc.Position.WithinDistance(player.Position)) continue;

            player.LocalNpcs.Add(npc);

            var delta = npc.Position.Delta(player.Position);
            pkt.WriteBits(14, npc.Index);
            pkt.WriteBits(5, delta.Y);
            pkt.WriteBits(5, delta.X);
            pkt.WriteBits(1, 0); // discard walking queue
            pkt.WriteBits(12, npc.Id);
            pkt.WriteBits(1, npc.UpdateRequired ? 1 : 0);

            if (npc.UpdateRequired)
                AppendUpdateBlock(blockData, npc);
        }

        // Terminator
        if (blockData.Position > 0)
        {
            pkt.WriteBits(14, 16383);
        }

        pkt.FinishBitAccess();

        if (blockData.Position > 0)
        {
            var blockBytes = blockData.BuildRaw();
            pkt.WriteBytes(blockBytes.Span);
        }

        var def = protocol.GetOutgoingByName("NpcUpdate");
        return def != null
            ? pkt.BuildVarShort(def.Opcode, session.OutgoingCipher)
            : ReadOnlyMemory<byte>.Empty;
    }

    private static void AppendUpdateBlock(PacketBuilder block, Npc npc)
    {
        int flags = 0;

        if (npc.AnimationUpdateRequired)        flags |= FlagAnimation;
        if (npc.HitUpdateRequired)              flags |= FlagHit;
        if (npc.GraphicUpdateRequired)          flags |= FlagGraphic;
        if (npc.FaceEntityUpdateRequired)       flags |= FlagFaceEntity;
        if (npc.ForceChatUpdateRequired)        flags |= FlagForcedChat;
        if (npc.FaceCoordinateUpdateRequired)   flags |= FlagFaceCoord;
        if (npc.TransformUpdateRequired)        flags |= FlagTransform;

        if (flags == 0) return;

        block.WriteByte(flags);

        if ((flags & FlagAnimation) != 0)
        {
            block.WriteLEShort(npc.AnimationId);
            block.WriteByte(npc.AnimationDelay);
        }

        if ((flags & FlagHit) != 0)
        {
            block.WriteByteC(npc.HitDamage);
            block.WriteByteS(npc.HitType);
            block.WriteByteS(npc.CurrentHealth);
            block.WriteByteC(npc.MaxHealth);
        }

        if ((flags & FlagGraphic) != 0)
        {
            block.WriteShort(npc.GraphicId);
            block.WriteInt(npc.GraphicHeight << 16 | npc.GraphicDelay);
        }

        if ((flags & FlagFaceEntity) != 0)
        {
            block.WriteShort(npc.FaceEntityIndex);
        }

        if ((flags & FlagForcedChat) != 0)
        {
            block.WriteString(npc.ForceChat ?? "");
        }

        if ((flags & FlagFaceCoord) != 0)
        {
            block.WriteLEShort(npc.FaceX);
            block.WriteLEShort(npc.FaceY);
        }

        if ((flags & FlagTransform) != 0)
        {
            block.WriteLEShortA(npc.TransformId);
        }
    }
}
