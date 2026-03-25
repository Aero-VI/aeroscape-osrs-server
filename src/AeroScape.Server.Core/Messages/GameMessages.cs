namespace AeroScape.Server.Core.Messages;

/// <summary>
/// Protocol-agnostic game messages. The engine operates on these records,
/// never on raw packet opcodes or byte buffers.
/// </summary>

// Movement
public readonly record struct WalkMessage(int DestX, int DestY, bool Running, IReadOnlyList<WalkStep> Steps);
public readonly record struct WalkStep(int DeltaX, int DeltaY);

// Chat
public readonly record struct PublicChatMessage(int Color, int Effect, byte[] PackedText);
public readonly record struct CommandMessage(string Command, string[] Arguments);

// Equipment & Inventory
public readonly record struct EquipItemMessage(int ItemId, int Slot, int InterfaceId);
public readonly record struct UnequipItemMessage(int Slot, int InterfaceId);
public readonly record struct MoveItemMessage(int InterfaceId, int FromSlot, int ToSlot);
public readonly record struct DropItemMessage(int ItemId, int Slot, int InterfaceId);

// Interaction
public readonly record struct PlayerInteractMessage(int TargetIndex, int OptionIndex);
public readonly record struct NpcInteractMessage(int NpcIndex, int OptionIndex);
public readonly record struct ObjectInteractMessage(int ObjectId, int X, int Y, int OptionIndex);
public readonly record struct GroundItemInteractMessage(int ItemId, int X, int Y);

// Interface / Button
public readonly record struct ButtonClickMessage(int InterfaceId, int ButtonId);
public readonly record struct DialogueContinueMessage(int InterfaceId, int ButtonId);
public readonly record struct CloseInterfaceMessage();

// Appearance
public readonly record struct AppearanceUpdateMessage(int Gender, int[] Look, int[] Colors);

// Misc
public readonly record struct KeepAliveMessage();
public readonly record struct RegionLoadedMessage();
public readonly record struct CameraMovedMessage(int Pitch, int Yaw);
public readonly record struct FocusChangedMessage(bool Focused);
public readonly record struct MouseClickMessage(int X, int Y, bool RightClick);
public readonly record struct IdleLogoutMessage();
