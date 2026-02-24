using UnityEngine;
using Unity.Netcode;
using Steamworks;
using Steamworks.Data;

// ✅ Ajuste si besoin selon ton transport
using Netcode.Transports.Facepunch;

public class SteamLobbyNetcode : MonoBehaviour
{
    [SerializeField] private int maxMembers = 4;

    private FacepunchTransport _transport;
    private Lobby? _currentLobby;

    void Start()
    {
        _transport = NetworkManager.Singleton.GetComponent<FacepunchTransport>();

        SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;
        SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
    }

    public async void Host()
    {
        if (!SteamBootstrap.Ready)
        {
            Debug.LogError("Steam pas prêt.");
            return;
        }

        Lobby? lobby = await SteamMatchmaking.CreateLobbyAsync(maxMembers);
        if (lobby == null)
        {
            Debug.LogError("CreateLobbyAsync a échoué.");
            return;
        }

        _currentLobby = lobby.Value;
        _currentLobby.Value.SetFriendsOnly();
        _currentLobby.Value.SetJoinable(true);
        _currentLobby.Value.SetData("name", $"{SteamClient.Name}'s Lobby");

        Debug.Log($"Lobby créé: {_currentLobby.Value.Id} owner={_currentLobby.Value.Owner.Id}");

        NetworkManager.Singleton.StartHost();
    }

    private async void OnGameLobbyJoinRequested(Lobby lobby, SteamId friend)
    {
        Debug.Log($"Join requested: lobby={lobby.Id} from friend={friend}");
        await lobby.Join();
        // La suite dans OnLobbyEntered
    }

    private void OnLobbyEntered(Lobby lobby)
    {
        Debug.Log($"Lobby entered: {lobby.Id} owner={lobby.Owner.Id}");

        _currentLobby = lobby;

        if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
            return;

        var hostSteamId = lobby.Owner.Id;
        _transport.targetSteamId = hostSteamId;

        NetworkManager.Singleton.StartClient();
    }

    public void Leave()
    {
        if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient)
            NetworkManager.Singleton.Shutdown();

        if (_currentLobby.HasValue)
        {
            _currentLobby.Value.Leave();
            _currentLobby = null;
        }
    }
}