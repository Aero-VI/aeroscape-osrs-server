namespace AeroScape.Server.Core.Messages.Outgoing;

/// <summary>
/// Update a single skill's level and experience for the client.
/// </summary>
public readonly record struct SendSkillMessage(int SkillId, int Level, int Experience);
