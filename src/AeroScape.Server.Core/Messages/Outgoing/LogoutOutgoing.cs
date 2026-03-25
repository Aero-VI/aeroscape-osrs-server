namespace AeroScape.Server.Core.Messages.Outgoing;

/// <summary>
/// Force-logout the client. No payload — opcode-only packet.
/// </summary>
public readonly record struct LogoutMessage();
