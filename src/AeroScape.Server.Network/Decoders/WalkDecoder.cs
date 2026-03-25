using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Protocol;

namespace AeroScape.Server.Network.Decoders;

/// <summary>
/// Decodes the three walk packets (opcodes 49, 119, 138).
/// Legacy Java Walking:
///   if (packetId == 119) packetSize -= 14;
///   numPath = (packetSize - 5) / 2
///   firstX = readUnsignedWordBigEndianA - regionOffset
///   firstY = readUnsignedWordA - regionOffset
///   running = readSignedByteC
///   for each path: readSignedByte (dx), readSignedByteS (dy)
///
/// We decode into protocol-agnostic coords (still region-relative at this level;
/// the handler converts to absolute using the player's current map region).
/// </summary>
public sealed class WalkDecoder : IPacketDecoder<WalkMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["Walk", "WalkOnCommand", "WalkMinimap"];

    public WalkMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);

        // WalkOnCommand (opcode 119) has 14 extra bytes of anti-cheat data at end
        int effectiveSize = packetName == "WalkOnCommand" ? data.Length - 14 : data.Length;
        int numPath = (effectiveSize - 5) / 2;

        int destX = reader.ReadUShort(); // readUnsignedWordBigEndianA
        int destY = reader.ReadShortA(); // readUnsignedWordA
        bool running = reader.ReadByteC() == 1; // readSignedByteC

        var steps = new List<WalkStep>();
        for (int i = 0; i < numPath; i++)
        {
            int dx = reader.ReadSignedByte();
            int dy = reader.ReadByteS();
            steps.Add(new WalkStep(dx, dy));
        }

        return new WalkMessage(destX, destY, running, steps);
    }
}
