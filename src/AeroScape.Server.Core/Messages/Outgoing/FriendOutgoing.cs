namespace AeroScape.Server.Core.Messages.Outgoing;

/// <summary>
/// Initialize the friends list loading state (0 = loading, 1 = connecting, 2 = loaded).
/// </summary>
public readonly record struct InitFriendListMessage(int Status);

/// <summary>
/// Update a single friend's online status and world.
/// </summary>
public readonly record struct FriendStatusMessage(long NameLong, int World);

/// <summary>
/// Forward the friend server information (for cross-world messaging).
/// </summary>
public readonly record struct SendFriendServerMessage(long NameLong, int World);

/// <summary>
/// Receive a private message from another player.
/// </summary>
public readonly record struct ReceivePrivateMessage(long SenderNameLong, int Rights, byte[] PackedText);
