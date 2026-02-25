using UnityEngine;
using Unity.Netcode;
using Steamworks;
using Steamworks.Data;

//  Adjust this if needed depending on your transport
using Netcode.Transports.Facepunch;

public class SteamLobbyNetcode : MonoBehaviour
{
    [SerializeField] private int maxMembers = 4; // Max players in the Steam lobby

    private FacepunchTransport _transport; // Netcode transport that connects via Steam P2P
    private Lobby? _currentLobby;          // Current Steam lobby (nullable)

    void Start()
    {
        // Get the Facepunch transport from the NetworkManager
        _transport = NetworkManager.Singleton.GetComponent<FacepunchTransport>();

        // Subscribe to Steam events:
        // - When a friend invites / requests you to join a lobby
        // - When you actually enter a lobby
        SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;
        SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
    }

    public async void Host()
    {
        // Make sure Steam is initialized and valid
        if (!SteamBootstrap.Ready)
        {
            Debug.LogError("Steam is not ready.");
            return;
        }

        // Create a Steam lobby
        Lobby? lobby = await SteamMatchmaking.CreateLobbyAsync(maxMembers);
        if (lobby == null)
        {
            Debug.LogError("CreateLobbyAsync failed.");
            return;
        }

        // Store the lobby reference
        _currentLobby = lobby.Value;

        // Configure lobby visibility / join rules
        _currentLobby.Value.SetFriendsOnly();
        _currentLobby.Value.SetJoinable(true);

        // Store a friendly lobby name in Steam lobby metadata
        _currentLobby.Value.SetData("name", $"{SteamClient.Name}'s Lobby");

        Debug.Log($"Lobby created: {_currentLobby.Value.Id} owner={_currentLobby.Value.Owner.Id}");

        // Start Netcode as Host (Server + Local Client)
        NetworkManager.Singleton.StartHost();
    }

    private async void OnGameLobbyJoinRequested(Lobby lobby, SteamId friend)
    {
        // Triggered when Steam asks us to join a lobby (e.g., friend invite)
        Debug.Log($"Join requested: lobby={lobby.Id} from friend={friend}");

        // Join the lobby asynchronously
        await lobby.Join();

        // The rest of the connection flow continues in OnLobbyEntered
    }

    private void OnLobbyEntered(Lobby lobby)
    {
        // Triggered when we successfully entered a Steam lobby
        Debug.Log($"Lobby entered: {lobby.Id} owner={lobby.Owner.Id}");

        // Store the lobby reference
        _currentLobby = lobby;

        // If we're already hosting/servering, do nothing
        if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
            return;

        // The lobby owner is considered the host for the session
        var hostSteamId = lobby.Owner.Id;

        // Tell the Facepunch transport which SteamId to connect to
        _transport.targetSteamId = hostSteamId;

        // Start Netcode as Client
        NetworkManager.Singleton.StartClient();
    }

    public void Leave()
    {
        // Stop Netcode networking if running
        if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient)
            NetworkManager.Singleton.Shutdown();

        // Leave the Steam lobby if we have one
        if (_currentLobby.HasValue)
        {
            _currentLobby.Value.Leave();
            _currentLobby = null;
        }
    }
}