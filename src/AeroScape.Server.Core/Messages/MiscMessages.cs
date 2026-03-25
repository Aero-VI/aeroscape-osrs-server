namespace AeroScape.Server.Core.Messages;

/// <summary>
/// Keep-alive / heartbeat ping.
/// </summary>
public readonly record struct KeepAliveMessage();

/// <summary>
/// Client has finished loading the map region.
/// </summary>
public readonly record struct RegionLoadedMessage();

/// <summary>
/// Camera angle changed.
/// </summary>
public readonly record struct CameraMovedMessage(int Pitch, int Yaw);

/// <summary>
/// Client window focus changed.
/// </summary>
public readonly record struct FocusChangedMessage(bool Focused);

/// <summary>
/// Mouse click event.
/// </summary>
public readonly record struct MouseClickMessage(int X, int Y, bool RightClick);

/// <summary>
/// Client idle logout notification.
/// </summary>
public readonly record struct IdleLogoutMessage();
