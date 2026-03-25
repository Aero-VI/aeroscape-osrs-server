namespace AeroScape.Server.Core.Messages;

/// <summary>
/// Add a friend to the friends list.
/// </summary>
public readonly record struct AddFriendMessage(long FriendNameLong);

/// <summary>
/// Remove a friend from the friends list.
/// </summary>
public readonly record struct RemoveFriendMessage(long FriendNameLong);

/// <summary>
/// Add a player to the ignore list.
/// </summary>
public readonly record struct AddIgnoreMessage(long IgnoreNameLong);

/// <summary>
/// Remove a player from the ignore list.
/// </summary>
public readonly record struct RemoveIgnoreMessage(long IgnoreNameLong);

/// <summary>
/// Send a private message to another player.
/// </summary>
public readonly record struct PrivateMessageMessage(long RecipientNameLong, byte[] PackedText);

/// <summary>
/// Join a clan chat channel.
/// </summary>
public readonly record struct JoinClanChatMessage(long ClanNameLong);
