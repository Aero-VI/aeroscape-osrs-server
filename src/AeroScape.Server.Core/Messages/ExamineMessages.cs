namespace AeroScape.Server.Core.Messages;

/// <summary>
/// Examine an item to see its description.
/// </summary>
public readonly record struct ExamineItemMessage(int ItemId);

/// <summary>
/// Examine an NPC to see its description.
/// </summary>
public readonly record struct ExamineNpcMessage(int NpcId);

/// <summary>
/// Examine a world object to see its description.
/// </summary>
public readonly record struct ExamineObjectMessage(int ObjectId);
