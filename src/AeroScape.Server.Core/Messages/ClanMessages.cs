namespace AeroScape.Server.Core.Messages;

/// <summary>
/// Kick a member from the current clan chat channel.
/// </summary>
public readonly record struct KickClanMemberMessage(long TargetNameLong);
