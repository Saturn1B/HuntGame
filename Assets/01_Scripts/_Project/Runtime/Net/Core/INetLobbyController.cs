namespace DungeonSteakhouse.Net.Core
{
    public interface INetLobbyController
    {
        void Host();
        void LeaveLobby();
        bool TryGetSessionId(out string sessionId);
    }
}
