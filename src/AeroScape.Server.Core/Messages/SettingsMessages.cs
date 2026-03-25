namespace AeroScape.Server.Core.Messages;

/// <summary>
/// A settings/options toggle button click (e.g., brightness, music volume, run mode).
/// </summary>
public readonly record struct SettingsButtonMessage(int SettingId, int Value);
