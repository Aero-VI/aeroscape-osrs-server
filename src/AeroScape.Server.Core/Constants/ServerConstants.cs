namespace AeroScape.Server.Core.Constants;

public static class ServerConstants
{
    public const int Revision = 508;
    public const int Port = 43594;
    public const int CycleRate = 600; // ms per game tick
    public const int MaxPlayers = 2048;
    public const int MaxNpcs = 32768;
    
    // Login response codes
    public const int LoginSuccess = 2;
    public const int LoginInvalidCredentials = 3;
    public const int LoginBanned = 4;
    public const int LoginAlreadyOnline = 5;
    public const int LoginServerUpdating = 14;
    public const int LoginWorldFull = 7;
    
    // Player rights
    public const int RightsNormal = 0;
    public const int RightsModerator = 1;
    public const int RightsAdmin = 2;
}
