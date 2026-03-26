using AeroScape.Server.Core.Crypto;
using AeroScape.Server.Core.Entities;
using AeroScape.Server.Network.Protocol;
using AeroScape.Server.Network.Session;
using System.Text;

namespace AeroScape.Server.Network.Updating;

/// <summary>
/// Builds the player update packet (opcode 81, var-short) for the 508 protocol.
/// This is the most complex packet — it handles movement, appearance blocks,
/// local list management, and all player update flags.
/// </summary>
public static class PlayerUpdatePacket
{
    // Update flag masks (508 protocol)
    private const int FlagGraphic       = 0x100;
    private const int FlagAnimation     = 0x8;
    private const int FlagForcedChat    = 0x4;
    private const int FlagChat          = 0x80;
    private const int FlagFaceEntity    = 0x1;
    private const int FlagAppearance    = 0x10;
    private const int FlagFaceCoord     = 0x2;
    private const int FlagHit           = 0x20;
    private const int FlagHit2          = 0x200;

    public static ReadOnlyMemory<byte> Build(PlayerSession session, ProtocolService protocol)
    {
        var player = session.Player;
        var pkt = new PacketBuilder(2048);
        var blockData = new PacketBuilder(4096);

        pkt.InitBitAccess();

        // --- This player's movement ---
        UpdateThisPlayerMovement(pkt, player);

        // --- Update local players ---
        pkt.WriteBits(8, player.LocalPlayers.Count);

        for (int i = player.LocalPlayers.Count - 1; i >= 0; i--)
        {
            var other = player.LocalPlayers[i];

            if (!other.IsActive || !other.Position.WithinDistance(player.Position) || other.IsTeleporting)
            {
                // Remove from local
                pkt.WriteBits(1, 1);
                pkt.WriteBits(2, 3); // remove
                player.LocalPlayers.RemoveAt(i);
            }
            else if (other.WalkDirection != -1 || other.RunDirection != -1)
            {
                // Movement update
                UpdateOtherPlayerMovement(pkt, other);
                if (other.UpdateRequired)
                    AppendUpdateBlock(blockData, other, false);
            }
            else if (other.UpdateRequired)
            {
                // No movement, but has update
                pkt.WriteBits(1, 1);
                pkt.WriteBits(2, 0);
                AppendUpdateBlock(blockData, other, false);
            }
            else
            {
                // No change
                pkt.WriteBits(1, 0);
            }
        }

        // --- Add new local players ---
        // Scan the world for players within view distance
        if (session.SessionManager != null)
        {
            foreach (var other in session.SessionManager.GetAll())
            {
                if (player.LocalPlayers.Count >= 255) break;
                if (other.Player == player) continue;
                if (!other.Player.IsActive) continue;
                if (player.LocalPlayers.Contains(other.Player)) continue;
                if (!other.Player.Position.WithinDistance(player.Position)) continue;

                player.LocalPlayers.Add(other.Player);
                AddNewPlayer(pkt, player, other.Player);
                AppendUpdateBlock(blockData, other.Player, true);
            }
        }

        // Terminator
        if (blockData.Position > 0)
        {
            pkt.WriteBits(11, 2047);
        }

        pkt.FinishBitAccess();

        // Append the update block data
        if (blockData.Position > 0)
        {
            // Copy block data bytes into main packet
            var blockBytes = blockData.BuildRaw();
            pkt.WriteBytes(blockBytes.Span);
        }

        var def = protocol.GetOutgoingByName("PlayerUpdate");
        return def != null
            ? pkt.BuildVarShort(def.Opcode, session.OutgoingCipher)
            : ReadOnlyMemory<byte>.Empty;
    }

    private static void UpdateThisPlayerMovement(PacketBuilder pkt, Player player)
    {
        if (player.IsTeleporting || player.NeedsMapRegionUpdate)
        {
            pkt.WriteBits(1, 1);     // update required
            pkt.WriteBits(2, 3);     // teleport
            pkt.WriteBits(2, player.Position.Z);
            pkt.WriteBits(1, player.IsTeleporting ? 1 : 0); // discard walking queue
            pkt.WriteBits(1, player.UpdateRequired ? 1 : 0);
            pkt.WriteBits(7, player.Position.LocalY);
            pkt.WriteBits(7, player.Position.LocalX);
        }
        else if (player.WalkDirection != -1)
        {
            if (player.RunDirection != -1)
            {
                // Running
                pkt.WriteBits(1, 1);
                pkt.WriteBits(2, 2);     // run
                pkt.WriteBits(3, player.WalkDirection);
                pkt.WriteBits(3, player.RunDirection);
                pkt.WriteBits(1, player.UpdateRequired ? 1 : 0);
            }
            else
            {
                // Walking
                pkt.WriteBits(1, 1);
                pkt.WriteBits(2, 1);     // walk
                pkt.WriteBits(3, player.WalkDirection);
                pkt.WriteBits(1, player.UpdateRequired ? 1 : 0);
            }
        }
        else if (player.UpdateRequired)
        {
            pkt.WriteBits(1, 1);
            pkt.WriteBits(2, 0);     // no movement, but flags
        }
        else
        {
            pkt.WriteBits(1, 0);     // nothing
        }
    }

    private static void UpdateOtherPlayerMovement(PacketBuilder pkt, Player other)
    {
        if (other.RunDirection != -1)
        {
            pkt.WriteBits(1, 1);
            pkt.WriteBits(2, 2);
            pkt.WriteBits(3, other.WalkDirection);
            pkt.WriteBits(3, other.RunDirection);
            pkt.WriteBits(1, other.UpdateRequired ? 1 : 0);
        }
        else
        {
            pkt.WriteBits(1, 1);
            pkt.WriteBits(2, 1);
            pkt.WriteBits(3, other.WalkDirection);
            pkt.WriteBits(1, other.UpdateRequired ? 1 : 0);
        }
    }

    private static void AddNewPlayer(PacketBuilder pkt, Player self, Player other)
    {
        pkt.WriteBits(11, other.Index);
        
        var delta = other.Position.Delta(self.Position);
        pkt.WriteBits(5, delta.Y);
        pkt.WriteBits(5, delta.X);
        
        pkt.WriteBits(1, 1); // update required
        pkt.WriteBits(1, 1); // discard walking queue
    }

    private static void AppendUpdateBlock(PacketBuilder block, Player player, bool forceAppearance)
    {
        int flags = 0;

        if (player.GraphicUpdateRequired)       flags |= FlagGraphic;
        if (player.AnimationUpdateRequired)     flags |= FlagAnimation;
        if (player.ForceChatUpdateRequired)     flags |= FlagForcedChat;
        if (player.ChatUpdateRequired)          flags |= FlagChat;
        if (player.FaceEntityUpdateRequired)    flags |= FlagFaceEntity;
        if (player.AppearanceUpdateRequired || forceAppearance) flags |= FlagAppearance;
        if (player.FaceCoordinateUpdateRequired) flags |= FlagFaceCoord;
        if (player.HitUpdateRequired)           flags |= FlagHit;
        if (player.Hit2UpdateRequired)          flags |= FlagHit2;

        if (flags == 0) return;

        if (flags >= 0x100)
        {
            flags |= 0x10; // Extended flag marker (from legacy: maskData |= 0x10)
            block.WriteByte(flags & 0xFF);
            block.WriteByte(flags >> 8);
        }
        else
        {
            block.WriteByte(flags);
        }

        // Order matters! Must match 508 client expectations.

        if ((flags & FlagGraphic) != 0)
        {
            block.WriteLEShort(player.GraphicId);
            block.WriteInt(player.GraphicHeight << 16 | player.GraphicDelay);
        }

        if ((flags & FlagAnimation) != 0)
        {
            block.WriteLEShort(player.AnimationId);
            block.WriteByteC(player.AnimationDelay);
        }

        if ((flags & FlagForcedChat) != 0)
        {
            block.WriteString(player.ForceChat ?? "");
        }

        if ((flags & FlagChat) != 0)
        {
            block.WriteLEShort(((player.ChatColor & 0xFF) << 8) | (player.ChatEffect & 0xFF));
            block.WriteByte(player.Rights);
            var chatData = player.ChatText ?? [];
            block.WriteByteC(chatData.Length);
            // Write chat data in reverse (508 protocol quirk)
            for (int i = chatData.Length - 1; i >= 0; i--)
                block.WriteByte(chatData[i]);
        }

        if ((flags & FlagFaceEntity) != 0)
        {
            block.WriteLEShort(player.FaceEntityIndex);
        }

        if ((flags & FlagAppearance) != 0)
        {
            AppendAppearanceBlock(block, player);
        }

        if ((flags & FlagFaceCoord) != 0)
        {
            block.WriteLEShortA(player.FaceX);
            block.WriteLEShort(player.FaceY);
        }

        if ((flags & FlagHit) != 0)
        {
            // From legacy PlayerUpdateMasks.appendHit1:
            // writeByteS(hitDiff1), writeByteS(hitType), writeByteS(hpRatio)
            block.WriteByteS(player.HitDamage);
            int hitType1 = player.HitDamage > 0 ? 1 : 0;
            block.WriteByteS(hitType1);
            // hpRatio = currentHP * 255 / maxHP (from legacy)
            int maxHp = player.Skills.GetLevelForExperience(player.Skills.GetExperience(3));
            int curHp = player.Skills.GetLevel(3);
            int hpRatio = maxHp > 0 ? curHp * 255 / maxHp : 0;
            block.WriteByteS(hpRatio);
        }

        if ((flags & FlagHit2) != 0)
        {
            // From legacy PlayerUpdateMasks.appendHit2:
            // writeByteS(hitDiff2), writeByteA(hitType)
            block.WriteByteS(player.Hit2Damage);
            int hitType2 = player.Hit2Damage > 0 ? 1 : 0;
            block.WriteByteA(hitType2);
        }
    }

    private static void AppendAppearanceBlock(PacketBuilder block, Player player)
    {
        var appearance = new PacketBuilder(128);
        var app = player.Appearance;
        var equip = player.Equipment;

        appearance.WriteByte(app.Gender);
        appearance.WriteByte(0); // overhead icon (skull/prayer)
        appearance.WriteByte(-1); // headicon pk

        // Equipment or model appearance
        // Slot order: hat, cape, amulet, weapon, chest, shield, (arms), legs, (hair), gloves, boots, (jaw), ring, ammo

        // Hat (slot 0)
        var hat = equip.Get(ItemDefinition.Slots.Hat);
        if (hat != null)
        {
            appearance.WriteShort(0x200 + hat.Id);
        }
        else
        {
            appearance.WriteByte(0);
        }

        // Cape (slot 1)
        var cape = equip.Get(ItemDefinition.Slots.Cape);
        if (cape != null)
        {
            appearance.WriteShort(0x200 + cape.Id);
        }
        else
        {
            appearance.WriteByte(0);
        }

        // Amulet (slot 2)
        var amulet = equip.Get(ItemDefinition.Slots.Amulet);
        if (amulet != null)
        {
            appearance.WriteShort(0x200 + amulet.Id);
        }
        else
        {
            appearance.WriteByte(0);
        }

        // Weapon (slot 3)
        var weapon = equip.Get(ItemDefinition.Slots.Weapon);
        if (weapon != null)
        {
            appearance.WriteShort(0x200 + weapon.Id);
        }
        else
        {
            appearance.WriteByte(0);
        }

        // Chest (slot 4)
        var chest = equip.Get(ItemDefinition.Slots.Chest);
        if (chest != null)
        {
            appearance.WriteShort(0x200 + chest.Id);
        }
        else
        {
            appearance.WriteShort(0x100 + app.Look[2]); // torso model
        }

        // Shield (slot 5)
        var shield = equip.Get(ItemDefinition.Slots.Shield);
        if (shield != null)
        {
            appearance.WriteShort(0x200 + shield.Id);
        }
        else
        {
            appearance.WriteByte(0);
        }

        // Arms — show if no platebody
        if (chest != null)
        {
            appearance.WriteShort(0x200 + chest.Id); // arms follow chest
        }
        else
        {
            appearance.WriteShort(0x100 + app.Look[3]); // arms model
        }

        // Legs (slot 7)
        var legs = equip.Get(ItemDefinition.Slots.Legs);
        if (legs != null)
        {
            appearance.WriteShort(0x200 + legs.Id);
        }
        else
        {
            appearance.WriteShort(0x100 + app.Look[5]); // legs model
        }

        // Head/hair — show if no full helm
        if (hat != null)
        {
            appearance.WriteByte(0);
        }
        else
        {
            appearance.WriteShort(0x100 + app.Look[0]); // head model
        }

        // Gloves (slot 9)
        var gloves = equip.Get(ItemDefinition.Slots.Gloves);
        if (gloves != null)
        {
            appearance.WriteShort(0x200 + gloves.Id);
        }
        else
        {
            appearance.WriteShort(0x100 + app.Look[4]); // hands model
        }

        // Boots (slot 10)
        var boots = equip.Get(ItemDefinition.Slots.Boots);
        if (boots != null)
        {
            appearance.WriteShort(0x200 + boots.Id);
        }
        else
        {
            appearance.WriteShort(0x100 + app.Look[6]); // feet model
        }

        // Jaw / Beard — show if no hat and male
        if (app.Gender == 0 && hat == null)
        {
            appearance.WriteShort(0x100 + app.Look[1]); // jaw/beard model
        }
        else
        {
            appearance.WriteByte(0);
        }

        // Colors
        for (int i = 0; i < 5; i++)
            appearance.WriteByte(app.Colors[i]);

        // Stand animations
        appearance.WriteShort(0x328); // standing
        appearance.WriteShort(0x337); // stand turn
        appearance.WriteShort(0x333); // walk
        appearance.WriteShort(0x334); // turn 180
        appearance.WriteShort(0x335); // turn 90 cw
        appearance.WriteShort(0x336); // turn 90 ccw
        appearance.WriteShort(0x338); // run

        appearance.WriteLong(NameToLong(player.Username));
        appearance.WriteByte(player.Skills.CombatLevel);
        appearance.WriteShort(0); // total level (for skill worlds)

        // Now write the appearance block with size prefix
        var appData = appearance.BuildRaw();
        block.WriteByteC(appData.Length);
        block.WriteBytes(appData.Span);
    }

    /// <summary>
    /// Converts a username to the RS2 long encoding (base 37).
    /// </summary>
    public static long NameToLong(string name)
    {
        long l = 0L;
        for (int i = 0; i < name.Length && i < 12; i++)
        {
            char c = name[i];
            l *= 37L;
            if (c >= 'A' && c <= 'Z') l += c - 'A' + 1;
            else if (c >= 'a' && c <= 'z') l += c - 'a' + 1;
            else if (c >= '0' && c <= '9') l += c - '0' + 27;
        }
        while (l % 37L == 0L && l != 0L)
            l /= 37L;
        return l;
    }

    /// <summary>
    /// Converts an RS2 long-encoded name back to a string.
    /// </summary>
    public static string LongToName(long l)
    {
        if (l <= 0L || l >= 0x5B5B57F8A98L) return "invalid_name";
        if (l % 37L == 0L) return "invalid_name";

        int len = 0;
        Span<char> chars = stackalloc char[12];
        while (l != 0L)
        {
            long next = l / 37L;
            int c = (int)(l - next * 37L);
            l = next;
            if (c >= 1 && c <= 26) chars[11 - len] = (char)('a' + c - 1);
            else if (c >= 27 && c <= 36) chars[11 - len] = (char)('0' + c - 27);
            else chars[11 - len] = '_';
            len++;
        }
        return new string(chars.Slice(12 - len, len));
    }
}
