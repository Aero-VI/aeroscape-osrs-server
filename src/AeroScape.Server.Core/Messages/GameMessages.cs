namespace AeroScape.Server.Core.Messages;

/// <summary>
/// Protocol-agnostic game messages. The engine operates on these records,
/// never on raw packet opcodes or byte buffers.
/// </summary>

// ── Movement ──────────────────────────────────────────────────
public readonly record struct WalkMessage(int DestX, int DestY, bool Running, IReadOnlyList<WalkStep> Steps);
public readonly record struct WalkStep(int DeltaX, int DeltaY);

// ── Chat ──────────────────────────────────────────────────────
public readonly record struct PublicChatMessage(int Color, int Effect, byte[] PackedText);
public readonly record struct CommandMessage(string Command, string[] Arguments);

// ── Equipment & Inventory ─────────────────────────────────────
public readonly record struct EquipItemMessage(int ItemId, int Slot, int InterfaceId);
public readonly record struct UnequipItemMessage(int Slot, int InterfaceId);
public readonly record struct MoveItemMessage(int InterfaceId, int FromSlot, int ToSlot);
public readonly record struct DropItemMessage(int ItemId, int Slot, int InterfaceId);
public readonly record struct ItemOperateMessage(int ItemId, int Slot, int InterfaceHash);
public readonly record struct ItemOption1Message(int ItemId, int Slot, int InterfaceId);
public readonly record struct ItemOption2Message(int ItemId, int Slot, int InterfaceId);
public readonly record struct ItemSelectMessage(int ItemId, int Slot, int InterfaceId);
public readonly record struct SwitchItemMessage(int FromSlot, int ToSlot, int InterfaceId);
public readonly record struct SwitchItemExtendedMessage(int FromSlot, int ToSlot, int FromInterfaceHash, int ToInterfaceHash);

// ── Item-on-X Interactions ────────────────────────────────────
public readonly record struct ItemOnItemMessage(int UsedItemId, int UsedWithItemId);
public readonly record struct ItemOnNpcMessage(int ItemId, int NpcIndex, int ItemSlot, int InterfaceId);
public readonly record struct ItemOnObjectMessage(int ItemId, int ObjectId, int ObjectX, int ObjectY);
public readonly record struct ItemOnPlayerMessage(int ItemId, int TargetIndex);

// ── Player Interaction ────────────────────────────────────────
public readonly record struct PlayerInteractMessage(int TargetIndex, int OptionIndex);

// ── NPC Interaction ───────────────────────────────────────────
public readonly record struct NpcInteractMessage(int NpcIndex, int OptionIndex);
public readonly record struct NpcAttackMessage(int NpcIndex);

// ── Object Interaction ────────────────────────────────────────
public readonly record struct ObjectInteractMessage(int ObjectId, int X, int Y, int OptionIndex);

// ── Ground Item ───────────────────────────────────────────────
public readonly record struct GroundItemInteractMessage(int ItemId, int X, int Y);

// ── Magic ─────────────────────────────────────────────────────
public readonly record struct MagicOnNpcMessage(int NpcIndex, int SpellId, int InterfaceId);
public readonly record struct MagicOnPlayerMessage(int TargetIndex, int SpellId, int InterfaceId);
public readonly record struct MagicOnItemMessage(int ItemId, int Slot, int SpellId, int InterfaceId);

// ── Interface / Button ────────────────────────────────────────
public readonly record struct ButtonClickMessage(int InterfaceId, int ButtonId);
public readonly record struct DialogueContinueMessage(int InterfaceId, int ButtonId);
public readonly record struct CloseInterfaceMessage();

// ── Appearance ────────────────────────────────────────────────
public readonly record struct AppearanceUpdateMessage(int Gender, int[] Look, int[] Colors);

// ── Friends / Ignore ──────────────────────────────────────────
public readonly record struct AddFriendMessage(long FriendNameLong);
public readonly record struct RemoveFriendMessage(long FriendNameLong);
public readonly record struct AddIgnoreMessage(long IgnoreNameLong);
public readonly record struct RemoveIgnoreMessage(long IgnoreNameLong);
public readonly record struct PrivateMessageMessage(long RecipientNameLong, byte[] PackedText);

// ── Clan Chat ─────────────────────────────────────────────────
public readonly record struct JoinClanChatMessage(long ClanNameLong);

// ── Examine ───────────────────────────────────────────────────
public readonly record struct ExamineItemMessage(int ItemId);
public readonly record struct ExamineNpcMessage(int NpcId);
public readonly record struct ExamineObjectMessage(int ObjectId);

// ── Input ─────────────────────────────────────────────────────
public readonly record struct NumberInputMessage(int Value);
public readonly record struct StringInputMessage(string Value);
public readonly record struct LongInputMessage(long Value, int InputId);

// ── Misc ──────────────────────────────────────────────────────
public readonly record struct KeepAliveMessage();
public readonly record struct RegionLoadedMessage();
public readonly record struct CameraMovedMessage(int Pitch, int Yaw);
public readonly record struct FocusChangedMessage(bool Focused);
public readonly record struct MouseClickMessage(int X, int Y, bool RightClick);
public readonly record struct IdleLogoutMessage();
