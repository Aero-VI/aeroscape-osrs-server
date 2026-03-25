namespace AeroScape.Server.Core.Messages;

/// <summary>
/// A button/action bar click on an interface.
/// </summary>
public readonly record struct ButtonClickMessage(int InterfaceId, int ButtonId);

/// <summary>
/// Continue/advance a dialogue interface.
/// </summary>
public readonly record struct DialogueContinueMessage(int InterfaceId, int ButtonId);

/// <summary>
/// Close the currently open interface.
/// </summary>
public readonly record struct CloseInterfaceMessage();
