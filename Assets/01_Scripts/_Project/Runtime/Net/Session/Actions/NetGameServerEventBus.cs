using System;
using UnityEngine;

public enum NetGameServerEventType
{
    StartRun = 0,
    ReturnToLobby = 1
}

public readonly struct NetGameServerEvent
{
    public readonly NetGameServerEventType Type;
    public readonly ulong SenderClientId;
    public readonly NetInteractable Source;
    public readonly Unity.Netcode.NetworkObject Interactor;

    public NetGameServerEvent(NetGameServerEventType type, ulong senderClientId, NetInteractable source, Unity.Netcode.NetworkObject interactor)
    {
        Type = type;
        SenderClientId = senderClientId;
        Source = source;
        Interactor = interactor;
    }
}

public static class NetGameServerEventBus
{
    public static event Action<NetGameServerEvent> OnEvent;

    public static void Raise(in NetGameServerEvent evt)
    {
        OnEvent?.Invoke(evt);
    }
}