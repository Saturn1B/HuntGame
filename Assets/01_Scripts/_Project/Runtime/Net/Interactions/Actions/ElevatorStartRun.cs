using UnityEngine;
using DungeonSteakhouse.Net.Session;

namespace DungeonSteakhouse.Net.Interactions.Actions
{
    [CreateAssetMenu(menuName = "DungeonSteakhouse/Interaction/Actions/Elevator Start Run")]
    public sealed class ElevatorStartRun : InteractionAction
    {
        [SerializeField] private NetGameServerEventType eventType = NetGameServerEventType.StartRun;

        [SerializeField] private bool logWhenDenied = true;

        public override bool CanExecute(in InteractionContext ctx)
        {
            if (ctx.Target == null)
                return false;

            var platform = ctx.Target.GetComponentInParent<NetLobbyReadyPlatform>();
            return platform != null && platform.AllReadyConfirmedServer;
        }

        public override void Execute(in InteractionContext ctx)
        {
            var platform = ctx.Target.GetComponentInParent<NetLobbyReadyPlatform>();
            if (platform == null)
            {
                if (logWhenDenied)
                    Debug.LogWarning("[Server] ElevatorStartRun denied: NetLobbyReadyPlatform not found in parents.");
                return;
            }

            if (!platform.AllReadyConfirmedServer)
            {
                if (logWhenDenied)
                    Debug.Log("[Server] ElevatorStartRun denied: AllReadyConfirmedServer is false.");
                return;
            }

            Debug.Log($"[Server] ElevatorButton accepted -> {eventType} (client {ctx.InteractorClientId}).");
            var evt = new NetGameServerEvent(eventType, ctx.InteractorClientId, ctx.Target, ctx.Interactor);
            NetGameServerEventBus.Raise(in evt);
        }
    }
}