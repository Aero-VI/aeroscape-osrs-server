namespace AeroScape.Server.Core.Messages.Outgoing;

/// <summary>
/// Open an interface (chatbox, main screen, inventory overlay, etc.).
/// </summary>
public readonly record struct SetInterfaceMessage(int InterfaceId, int WindowId, int ChildId);

/// <summary>
/// Set a sidebar tab's interface (inventory, prayer, magic, etc.).
/// </summary>
public readonly record struct SetSidebarMessage(int InterfaceId, int TabId);
