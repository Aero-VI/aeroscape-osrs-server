using System.Text.Json.Serialization;

namespace AeroScape.Server.Network.Protocol;

/// <summary>
/// JSON-driven protocol definition. Loaded from Protocol_508.json at startup.
/// No hardcoded opcodes anywhere.
/// </summary>
public sealed class ProtocolDefinition
{
    [JsonPropertyName("revision")]
    public int Revision { get; set; }

    [JsonPropertyName("incoming")]
    public Dictionary<string, PacketDefinition> Incoming { get; set; } = new();

    [JsonPropertyName("outgoing")]
    public Dictionary<string, PacketDefinition> Outgoing { get; set; } = new();
}

public sealed class PacketDefinition
{
    [JsonPropertyName("opcode")]
    public int Opcode { get; set; }

    [JsonPropertyName("size")]
    public int Size { get; set; } // -1 = var byte, -2 = var short, >= 0 = fixed

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
