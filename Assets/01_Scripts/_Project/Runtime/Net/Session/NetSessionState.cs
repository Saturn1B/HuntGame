namespace DungeonSteakhouse.Net.Session
{
    public enum NetSessionState : byte
    {
        Lobby = 0,
        StartingRun = 1,
        InRun = 2,
        ReturningToLobby = 3
    }
}
