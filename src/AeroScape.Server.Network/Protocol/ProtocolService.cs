using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Network.Protocol;

/// <summary>
/// Singleton service that provides opcode-to-name and size lookups.
/// Reads Protocol_508.json on startup.
/// </summary>
public sealed class ProtocolService
{
    private readonly Dictionary<int, PacketDefinition> _incomingByOpcode = new();
    private readonly Dictionary<string, PacketDefinition> _incomingByName = new();
    private readonly Dictionary<int, PacketDefinition> _outgoingByOpcode = new();
    private readonly Dictionary<string, PacketDefinition> _outgoingByName = new();
    private readonly ILogger<ProtocolService> _logger;

    public ProtocolService(ILogger<ProtocolService> logger)
    {
        _logger = logger;
    }

    public async Task LoadAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Protocol definition file not found: {filePath}");

        await using var stream = File.OpenRead(filePath);
        var def = await JsonSerializer.DeserializeAsync<ProtocolDefinition>(stream, cancellationToken: ct)
            ?? throw new InvalidOperationException("Failed to deserialize protocol definition.");

        foreach (var (name, pkt) in def.Incoming)
        {
            pkt.Name = name;
            _incomingByOpcode[pkt.Opcode] = pkt;
            _incomingByName[name] = pkt;
        }

        foreach (var (name, pkt) in def.Outgoing)
        {
            pkt.Name = name;
            _outgoingByOpcode[pkt.Opcode] = pkt;
            _outgoingByName[name] = pkt;
        }

        _logger.LogInformation(
            "Loaded protocol revision {Rev}: {In} incoming, {Out} outgoing packets",
            def.Revision, _incomingByOpcode.Count, _outgoingByOpcode.Count);
    }

    public PacketDefinition? GetIncoming(int opcode) =>
        _incomingByOpcode.GetValueOrDefault(opcode);

    public PacketDefinition? GetIncomingByName(string name) =>
        _incomingByName.GetValueOrDefault(name);

    public PacketDefinition? GetOutgoing(int opcode) =>
        _outgoingByOpcode.GetValueOrDefault(opcode);

    public PacketDefinition? GetOutgoingByName(string name) =>
        _outgoingByName.GetValueOrDefault(name);

    public int GetIncomingSize(int opcode) =>
        _incomingByOpcode.TryGetValue(opcode, out var pkt) ? pkt.Size : 0;
}
