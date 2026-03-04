using UnityEngine;
using Unity.Netcode;

public class NetGameSessionServerEventRouter : NetworkBehaviour
{
    [Header("Assign your session here")]
    [SerializeField] private MonoBehaviour netGameSessionBehaviour;

    private INetGameSession _session;

    private void Awake()
    {
        _session = netGameSessionBehaviour as INetGameSession;
        if (netGameSessionBehaviour != null && _session == null)
        {
            Debug.LogError("[NetGameSessionServerEventRouter] Assigned behaviour does not implement INetGameSession.");
        }
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        NetGameServerEventBus.OnEvent += OnServerEvent;
        Debug.Log("[NetGameSessionServerEventRouter] Subscribed to server event bus.");
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer)
            return;

        NetGameServerEventBus.OnEvent -= OnServerEvent;
    }

    private void OnServerEvent(NetGameServerEvent evt)
    {
        if (_session == null)
        {
            Debug.LogError("[NetGameSessionServerEventRouter] No session assigned.");
            return;
        }

        switch (evt.Type)
        {
            case NetGameServerEventType.StartRun:
                _session.RequestStartRun(evt.SenderClientId);
                break;

            case NetGameServerEventType.ReturnToLobby:
                _session.ReturnToLobby(evt.SenderClientId);
                break;
        }
    }
}

public interface INetGameSession
{
    void RequestStartRun(ulong senderClientId);
    void ReturnToLobby(ulong senderClientId);
}